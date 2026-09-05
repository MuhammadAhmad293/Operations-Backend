using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Transaction;
using Meezan.Dto.DTOs.Wallet;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IJob;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CategoryDefaults;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.WalletService
{
    public class WalletService : BaseService, IWalletService
    {
        private IRateService RateService { get; }
        private IJobEnqueuer JobEnqueuer { get; }
        private ITransactionService TransactionService { get; }
        private IZakatEngine ZakatEngine { get; }

        public WalletService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IRateService rateService, IJobEnqueuer jobEnqueuer, ITransactionService transactionService, IZakatEngine zakatEngine)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
            JobEnqueuer = jobEnqueuer;
            TransactionService = transactionService;
            ZakatEngine = zakatEngine;
        }

        #region Public Methods

        public async Task<ResponseDto<List<WalletDto>>> GetAll(string? userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<WalletDto>> response = new ResponseDto<List<WalletDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountAsync(account.Id);

            List<WalletDto> dtos = new();
            foreach (Wallet wallet in wallets)
            {
                WalletDto dto = Mapper.Map<WalletDto>(wallet);
                dto.Balance = await GetWalletBalanceAsync(wallet);
                dtos.Add(dto);
            }

            return response.GetSuccessResponse(dtos);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateWalletDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CurrencyCode))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (!await UnitOfWork.CurrencyRepository.AnyAsync(c => c.Code == request.CurrencyCode))
                throw new InvalidRequestException(Localization.InvalidBaseCurrency);

            if (!await UnitOfWork.WalletTypeRepository.AnyAsync(wt => wt.WalletTypeId == request.WalletTypeId))
                throw new InvalidRequestException(Localization.InvalidRequest);

            Wallet wallet = new()
            {
                AccountId = account.Id,
                WalletTypeId = request.WalletTypeId,
                Name = request.Name,
                CurrencyCode = request.CurrencyCode,
                InitialAmount = request.InitialAmount ?? 0m,
                Color = request.Color,
                Icon = request.Icon,
                ExcludeFromTotal = request.ExcludeFromTotal,
                IsArchived = false,
            };
            UnitOfWork.WalletRepository.Create(wallet);

            // BR-19: never fetch rates on a user action — check for existing data and, if none,
            // hand off to a background job (Phase 016 sub-task 3) instead of calling the rate
            // provider inline here. The direct/inverse/cross-through-USD resolution chain covers
            // the gap for this currency until that job lands.
            bool needsRateSync = !await RateService.HasRecentSnapshotAsync(request.CurrencyCode, cancellationToken);

            if (await UnitOfWork.CommitAsync(cancellationToken) <= default(int))
                return response.GetErrorResponse(Localization.GeneralError);

            if (needsRateSync)
                JobEnqueuer.EnqueueRateSync();

            return response.GetSuccessResponse(Localization.GeneralSuccess);
        }

        // Mode A ("Adjust by transaction", Phase 017): reconciles a wallet's balance to
        // request.NewBalance by posting a real Income/Expense transaction for the delta, through
        // the same transaction-creation path every other transaction uses (AddAdjustment reuses
        // TransactionService's private ValidateAndResolveAsync). Never mutates stored history —
        // every derived balance (including past statistics periods) stays exactly as it was.
        public async Task<ResponseDto<EmptyResponseDto>> AdjustBalance(string? userId, AdjustWalletBalanceDto request, CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            (Account account, Wallet wallet, decimal delta) = await ResolveAdjustmentAsync(userId, request.Id, request.NewBalance);

            TransactionType type = delta > 0m ? TransactionType.Income : TransactionType.Expense;
            CategoryKind kind = delta > 0m ? CategoryKind.Income : CategoryKind.Expense;

            Category category = await GetOrCreateBalanceAdjustmentCategoryAsync(account, kind, cancellationToken);

            DateTime now = DateTime.UtcNow;
            CreateTransactionDto adjustmentRequest = new()
            {
                Type = type.ToString(),
                DateGregorian = DateOnly.FromDateTime(now),
                Time = TimeOnly.FromDateTime(now),
                Amount = Math.Abs(delta),
                WalletId = wallet.Id,
                CategoryId = category.Id,
                Note = request.Note,
            };

            return await TransactionService.AddAdjustment(userId, adjustmentRequest, cancellationToken);
        }

        // Mode B ("Change initial amount", Phase 017): corrects Wallet.InitialAmount directly by
        // the same delta Mode A would otherwise turn into a transaction — no transaction, no
        // history entry, a quiet correction. Retroactive by design: every balance derived from
        // this wallet (including past statistics periods) shifts by the same amount, since
        // GetWalletBalanceAsync always computes InitialAmount + signed sum live, never from a
        // cached figure. InitialAmount is otherwise immutable post-creation (UpdateWalletDto has
        // no such field) — this is the only path that may ever change it.
        public async Task<ResponseDto<EmptyResponseDto>> SetInitialAmount(string? userId, SetWalletInitialAmountDto request, CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            (Account account, Wallet wallet, decimal delta) = await ResolveAdjustmentAsync(userId, request.Id, request.NewBalance);

            wallet.InitialAmount += delta;

            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                UnitOfWork.WalletRepository.Update(wallet);
                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.InitialAmountUpdated)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateWalletDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CurrencyCode))
                throw new InvalidRequestException(Localization.InvalidRequest);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == request.Id && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            if (!string.Equals(wallet.CurrencyCode, request.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                if (await UnitOfWork.TransactionRepository.ExistsForWalletAsync(wallet.Id))
                    throw new InvalidRequestException(Localization.WalletCurrencyLocked);

                if (!await UnitOfWork.CurrencyRepository.AnyAsync(c => c.Code == request.CurrencyCode))
                    throw new InvalidRequestException(Localization.InvalidBaseCurrency);

                wallet.CurrencyCode = request.CurrencyCode;
            }

            if (!await UnitOfWork.WalletTypeRepository.AnyAsync(wt => wt.WalletTypeId == request.WalletTypeId))
                throw new InvalidRequestException(Localization.InvalidRequest);

            wallet.Name = request.Name;
            wallet.WalletTypeId = request.WalletTypeId;
            wallet.Color = request.Color;
            wallet.Icon = request.Icon;
            wallet.ExcludeFromTotal = request.ExcludeFromTotal;
            UnitOfWork.WalletRepository.Update(wallet);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Archive(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == id && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            decimal balance = await GetWalletBalanceAsync(wallet);
            if (balance != 0m)
                throw new UnprocessableEntityException(Localization.WalletBalanceNotZero);

            wallet.IsArchived = true;
            UnitOfWork.WalletRepository.Update(wallet);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.WalletArchived)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == id && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            if (await UnitOfWork.TransactionRepository.ExistsForWalletAsync(wallet.Id))
            {
                wallet.IsDeleted = true;
                UnitOfWork.WalletRepository.Update(wallet);
            }
            else
            {
                UnitOfWork.WalletRepository.Delete(wallet);
            }

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion

        #region Private Methods

        // Shared by both adjustment modes: resolve + validate the wallet, then compute the delta
        // between its current computed balance and the requested one. Both modes reject the same
        // two conditions the same way (404 missing/deleted, 422 archived, 422 zero delta) — the
        // only difference between modes is what they each do *with* the resulting delta.
        private async Task<(Account Account, Wallet Wallet, decimal Delta)> ResolveAdjustmentAsync(string? userId, int walletId, decimal newBalance)
        {
            Account account = await GetAccountByUserIdAsync(userId);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == walletId && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            if (wallet.IsArchived)
                throw new UnprocessableEntityException(Localization.WalletIsArchived);

            decimal currentBalance = await GetWalletBalanceAsync(wallet);
            decimal delta = newBalance - currentBalance;

            if (delta == 0m)
                throw new UnprocessableEntityException(Localization.NoBalanceChange);

            return (account, wallet, delta);
        }

        // Finds the account's protected Balance Adjustment category for the given kind, creating
        // it on first use. Covers both account-creation paths: brand-new accounts already have it
        // from AccountService's DefaultCategoryTemplate (this returns the existing row instantly),
        // while accounts created before this feature shipped get it lazily, right here, the first
        // time they actually need one — no backfill migration required for every existing account.
        // Commits immediately when creating, since the caller needs a real (persisted) CategoryId
        // to hand to TransactionService.AddAdjustment right after.
        private async Task<Category> GetOrCreateBalanceAdjustmentCategoryAsync(Account account, CategoryKind kind, CancellationToken cancellationToken)
        {
            Category? category = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(
                c => c.AccountId == account.Id && c.SystemPurpose == CategorySystemPurpose.BalanceAdjustment && c.Kind == kind && !c.IsDeleted);

            if (category is not null)
                return category;

            category = new Category
            {
                Account = account,
                Kind = kind,
                Name = account.Language == Language.Ar ? BalanceAdjustmentCategoryName.Ar : BalanceAdjustmentCategoryName.En,
                IsProtected = true,
                SystemPurpose = CategorySystemPurpose.BalanceAdjustment,
            };
            UnitOfWork.CategoryRepository.Create(category);
            await UnitOfWork.CommitAsync(cancellationToken);

            return category;
        }

        #endregion
    }
}
