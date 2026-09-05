using Common.CrossCurrencyConversion;
using Common.Dto;
using Common.FileStorage;
using Common.GoldPurity;
using Common.HijriCalendar;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Transaction;
using Meezan.IRepositories.IRepository;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Meezan.Services.TransactionService
{
    public class TransactionService : BaseService, ITransactionService
    {
        private const string GoldCurrencyCode = "GOLD";

        private IRateService RateService { get; }
        private IHijriCalendarHelper HijriCalendarHelper { get; }
        private IZakatEngine ZakatEngine { get; }
        private IFileStorageService FileStorageService { get; }
        private IGoldPurityCalculator GoldPurityCalculator { get; }
        private ICrossCurrencyConversionResolver ConversionResolver { get; }
        private ILogger<TransactionService> Logger { get; }

        public TransactionService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IRateService rateService, IHijriCalendarHelper hijriCalendarHelper, IZakatEngine zakatEngine,
            IFileStorageService fileStorageService, IGoldPurityCalculator goldPurityCalculator,
            ICrossCurrencyConversionResolver conversionResolver, ILogger<TransactionService> logger)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
            HijriCalendarHelper = hijriCalendarHelper;
            ZakatEngine = zakatEngine;
            FileStorageService = fileStorageService;
            GoldPurityCalculator = goldPurityCalculator;
            ConversionResolver = conversionResolver;
            Logger = logger;
        }

        #region Public Methods

        public async Task<ResponseDto<List<TransactionGroupDto>>> GetFiltered(string? userId, string? period, DateOnly? from, DateOnly? to, int? walletId, int? categoryId, string? type, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<TransactionGroupDto>> response = new ResponseDto<List<TransactionGroupDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            TransactionFilter filter = new()
            {
                WalletId = walletId,
                CategoryId = categoryId,
            };

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse(type, true, out TransactionType parsedType))
                filter.Type = parsedType;

            (filter.From, filter.To) = ResolvePeriod(period, from, to);

            List<Transaction> transactions = await UnitOfWork.TransactionRepository.GetFilteredAsync(account.Id, filter);

            return response.GetSuccessResponse(GroupByDay(transactions));
        }

        public async Task<ResponseDto<List<TransactionGroupDto>>> Search(string? userId, string query, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<TransactionGroupDto>> response = new ResponseDto<List<TransactionGroupDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidRequestException(Localization.InvalidRequest);

            List<Transaction> transactions = await UnitOfWork.TransactionRepository.SearchAsync(account.Id, query);

            return response.GetSuccessResponse(GroupByDay(transactions));
        }

        public async Task<ResponseDto<TransactionDto>> GetById(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<TransactionDto> response = new ResponseDto<TransactionDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Transaction transaction = await UnitOfWork.TransactionRepository.FirstOrDefaultAsync(t => t.Id == id && t.AccountId == account.Id && !t.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.TransactionNotFound);

            TransactionDto dto = Mapper.Map<TransactionDto>(transaction);

            // BR-06: even a soft-deleted wallet/category must still resolve its display name
            // here, since it remains the transaction's *current* selected value (just absent
            // from GET /api/wallets or /api/categories — deliberately unfiltered by IsDeleted).
            Wallet? wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == transaction.WalletId);
            dto.WalletName = wallet?.Name;

            if (transaction.ToWalletId.HasValue)
            {
                Wallet? toWallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == transaction.ToWalletId.Value);
                dto.ToWalletName = toWallet?.Name;
            }

            if (transaction.CategoryId.HasValue)
            {
                Category? category = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.Id == transaction.CategoryId.Value);
                dto.CategoryName = category?.Name;
            }

            return response.GetSuccessResponse(dto);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateTransactionDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            ResolvedTransaction resolved = await ValidateAndResolveAsync(account, request.Type, request.Amount, request.DateGregorian,
                request.WalletId, request.ToWalletId, request.CategoryId, request.Karat, request.ExchangeRate, request.ConvertedAmount, cancellationToken);

            Transaction transaction = new()
            {
                Account = account,
                Type = resolved.Type,
                DateGregorian = request.DateGregorian,
                DateHijri = resolved.DateHijri,
                Time = request.Time,
                Amount = request.Amount,
                Wallet = resolved.Wallet,
                ToWallet = resolved.ToWallet,
                Category = resolved.Category,
                Karat = resolved.Karat,
                PureGoldGrams = resolved.PureGoldGrams,
                ExchangeRate = resolved.ExchangeRate,
                ConvertedAmount = resolved.ConvertedAmount,
                IsFee = false,
                Description = request.Description,
                Note = request.Note,
            };

            Transaction? fee = null;
            if (request.Fee is not null && request.Fee.Amount > 0)
            {
                (int? feeKarat, decimal? feePureGoldGrams) = ComputeKarat(resolved.Wallet.CurrencyCode, request.Fee.Amount, resolved.Karat);

                fee = new Transaction
                {
                    Account = account,
                    Type = TransactionType.Expense,
                    DateGregorian = request.DateGregorian,
                    DateHijri = resolved.DateHijri,
                    Time = request.Time,
                    Amount = request.Fee.Amount,
                    Wallet = resolved.Wallet,
                    Karat = feeKarat,
                    PureGoldGrams = feePureGoldGrams,
                    IsFee = true,
                    ParentTransaction = transaction,
                };
            }

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                UnitOfWork.TransactionRepository.Create(transaction);
                if (fee is not null)
                    UnitOfWork.TransactionRepository.Create(fee);

                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.TransactionSaved)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> AddAdjustment(string? userId, CreateTransactionDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            ResolvedTransaction resolved = await ValidateAndResolveAsync(account, request.Type, request.Amount, request.DateGregorian,
                request.WalletId, request.ToWalletId, request.CategoryId, request.Karat, request.ExchangeRate, request.ConvertedAmount, cancellationToken);

            Transaction transaction = new()
            {
                Account = account,
                Type = resolved.Type,
                DateGregorian = request.DateGregorian,
                DateHijri = resolved.DateHijri,
                Time = request.Time,
                Amount = request.Amount,
                Wallet = resolved.Wallet,
                Category = resolved.Category,
                Karat = resolved.Karat,
                PureGoldGrams = resolved.PureGoldGrams,
                IsFee = false,
                IsAdjustment = true,
                Description = request.Description,
                Note = request.Note,
            };

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                UnitOfWork.TransactionRepository.Create(transaction);
                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.TransactionSaved)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateTransactionDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            Transaction transaction = await UnitOfWork.TransactionRepository.FirstOrDefaultAsync(t => t.Id == request.Id && t.AccountId == account.Id && !t.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.TransactionNotFound);

            // BR-06: the NEW WalletId/CategoryId must reference a non-deleted row — once changed
            // away from a soft-deleted value, it can never be re-selected. No special-casing is
            // needed here: ValidateAndResolveAsync already scopes its lookups to `!IsDeleted`.
            ResolvedTransaction resolved = await ValidateAndResolveAsync(account, request.Type, request.Amount, request.DateGregorian,
                request.WalletId, request.ToWalletId, request.CategoryId, request.Karat, request.ExchangeRate, request.ConvertedAmount, cancellationToken);

            int? previousZakatCycleId = transaction.ZakatCycleId;

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                transaction.Type = resolved.Type;
                transaction.DateGregorian = request.DateGregorian;
                transaction.DateHijri = resolved.DateHijri;
                transaction.Time = request.Time;
                transaction.Amount = request.Amount;
                transaction.WalletId = resolved.Wallet.Id;
                transaction.ToWalletId = resolved.ToWallet?.Id;
                transaction.CategoryId = resolved.Category?.Id;
                transaction.Karat = resolved.Karat;
                transaction.PureGoldGrams = resolved.PureGoldGrams;
                transaction.ExchangeRate = resolved.ExchangeRate;
                transaction.ConvertedAmount = resolved.ConvertedAmount;
                transaction.Description = request.Description;
                transaction.Note = request.Note;
                UnitOfWork.TransactionRepository.Update(transaction);

                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
                if (previousZakatCycleId.HasValue)
                    await ZakatEngine.RecomputeCyclePaymentAsync(previousZakatCycleId.Value, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.TransactionSaved)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Transaction transaction = await UnitOfWork.TransactionRepository.FirstOrDefaultAsync(t => t.Id == id && t.AccountId == account.Id && !t.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.TransactionNotFound);

            // BR-09: the fee self-reference FK is Restrict at the DB level (SQL Server rejects
            // the cascade — Phase 005 sub-task 12), so the linked fee must be found and deleted
            // explicitly here rather than relying on a DB cascade.
            List<Transaction> linkedFees = await UnitOfWork.TransactionRepository.GetFeesByParentIdAsync(transaction.Id);

            // Collect every attachment's storage path (parent + fees) BEFORE the DB delete — the
            // DB cascade removes the Attachment rows, but never the physical files.
            List<int> transactionIds = new List<int> { transaction.Id }.Concat(linkedFees.Select(f => f.Id)).ToList();
            List<Attachment> attachments = await UnitOfWork.AttachmentRepository.GetByTransactionIdsAsync(transactionIds);

            int? zakatCycleId = transaction.ZakatCycleId;

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                foreach (Transaction fee in linkedFees)
                    UnitOfWork.TransactionRepository.Delete(fee);

                UnitOfWork.TransactionRepository.Delete(transaction);

                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
                if (zakatCycleId.HasValue)
                    await ZakatEngine.RecomputeCyclePaymentAsync(zakatCycleId.Value, cancellationToken);
            }, cancellationToken);

            // Best-effort physical file cleanup — the DB commit above is the source of truth;
            // a stray orphaned file on disk is logged, not allowed to fail the whole delete.
            foreach (Attachment attachment in attachments)
            {
                try
                {
                    await FileStorageService.DeleteAsync(attachment.StoragePath);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to delete physical file {StoragePath} for attachment {AttachmentId} after transaction {TransactionId} was deleted",
                        attachment.StoragePath, attachment.Id, id);
                }
            }

            return result > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion

        #region Private Methods

        private record ResolvedTransaction(TransactionType Type, Wallet Wallet, Wallet? ToWallet, Category? Category, int? Karat, decimal? PureGoldGrams, string DateHijri, decimal? ExchangeRate, decimal? ConvertedAmount);

        // Shared by Add and Update: BR-07 type-conditional validation, BR-03 karat computation,
        // BR-08 server-side Hijri conversion, BR-10 cross-currency resolution. Wallet/Category
        // lookups are always scoped to `!IsDeleted` — for Update this is exactly what enforces
        // BR-06's "once changed away, a soft-deleted value can never be re-selected" rule.
        private async Task<ResolvedTransaction> ValidateAndResolveAsync(Account account, string? typeStr, decimal amount, DateOnly dateGregorian,
            int walletId, int? toWalletId, int? categoryId, int? requestedKarat, decimal? requestedRate, decimal? requestedConvertedAmount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(typeStr) || !Enum.TryParse(typeStr, true, out TransactionType type))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (amount <= 0)
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (dateGregorian == default)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == walletId && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            Wallet? toWallet = null;
            Category? category = null;

            if (type == TransactionType.Transfer)
            {
                if (!toWalletId.HasValue)
                    throw new InvalidRequestException(Localization.InvalidRequest);

                if (toWalletId.Value == walletId)
                    throw new InvalidRequestException(Localization.WalletsMustDiffer);

                toWallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == toWalletId.Value && w.AccountId == account.Id && !w.IsDeleted)
                    ?? throw new ObjectNotFoundException(Localization.WalletNotFound);
            }
            else
            {
                if (!categoryId.HasValue)
                    throw new InvalidRequestException(Localization.InvalidRequest);

                category = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.Id == categoryId.Value && c.AccountId == account.Id && !c.IsDeleted)
                    ?? throw new ObjectNotFoundException(Localization.CategoryNotFound);

                CategoryKind expectedKind = type == TransactionType.Income ? CategoryKind.Income : CategoryKind.Expense;
                if (category.Kind != expectedKind)
                    throw new InvalidRequestException(Localization.CategoryKindMismatch);
            }

            (int? karat, decimal? pureGoldGrams) = ComputeKarat(wallet.CurrencyCode, amount, requestedKarat);

            string dateHijri = HijriCalendarHelper.ToHijriString(dateGregorian);

            (decimal? exchangeRate, decimal? convertedAmount) = ResolveConversion(wallet, toWallet, amount, requestedRate, requestedConvertedAmount);

            if (toWallet is not null && wallet.CurrencyCode != toWallet.CurrencyCode && !exchangeRate.HasValue)
            {
                exchangeRate = await RateService.GetLatestAsync(wallet.CurrencyCode, toWallet.CurrencyCode, cancellationToken);
                convertedAmount = amount * exchangeRate.Value;
            }

            return new ResolvedTransaction(type, wallet, toWallet, category, karat, pureGoldGrams, dateHijri, exchangeRate, convertedAmount);
        }

        private List<TransactionGroupDto> GroupByDay(List<Transaction> transactions)
            => transactions
                .GroupBy(t => t.DateGregorian)
                .OrderByDescending(g => g.Key)
                .Select(g => new TransactionGroupDto
                {
                    Date = g.Key,
                    Transactions = g.Select(t => Mapper.Map<TransactionDto>(t)).ToList(),
                })
                .ToList();

        // BR-03: karat only applies to GOLD wallets (Silver has no karat); the client-supplied
        // pure value is never trusted — always recomputed here.
        private (int? Karat, decimal? PureGoldGrams) ComputeKarat(string currencyCode, decimal amount, int? requestedKarat)
        {
            if (!string.Equals(currencyCode, GoldCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return (null, null);

            int karat = requestedKarat ?? 24;
            if (!GoldPurityCalculator.IsValidKarat(karat))
                throw new InvalidRequestException(Localization.InvalidRequest);

            decimal pureGoldGrams = GoldPurityCalculator.ToPureGoldGrams(amount, karat);
            return (karat, pureGoldGrams);
        }

        // BR-10 override math itself lives in the pure, unit-tested CrossCurrencyConversionResolver
        // (Common/CrossCurrencyConversion) — this just decides whether a transfer is cross-currency
        // at all, the one piece of the decision that needs the Wallet entities.
        private (decimal? ExchangeRate, decimal? ConvertedAmount) ResolveConversion(Wallet wallet, Wallet? toWallet, decimal amount, decimal? requestedRate, decimal? requestedConvertedAmount)
        {
            bool isCrossCurrencyTransfer = toWallet is not null && wallet.CurrencyCode != toWallet.CurrencyCode;
            return ConversionResolver.Resolve(isCrossCurrencyTransfer, amount, requestedRate, requestedConvertedAmount);
        }

        #endregion
    }
}
