using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Wallet;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IJob;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.WalletService
{
    public class WalletService : BaseService, IWalletService
    {
        private IRateService RateService { get; }
        private IJobEnqueuer JobEnqueuer { get; }

        public WalletService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IRateService rateService, IJobEnqueuer jobEnqueuer)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
            JobEnqueuer = jobEnqueuer;
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
    }
}
