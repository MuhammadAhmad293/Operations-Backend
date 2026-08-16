using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Account;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;
using System.Globalization;

namespace Meezan.Services.AccountService
{
    public class AccountService : BaseService, IAccountService
    {
        private const string FallbackFiatCurrencyCode = "USD";
        private const string CashWalletTypeName = "Cash";

        private static readonly (CategoryKind Kind, string EnName, string ArName, bool IsProtected)[] DefaultCategoryTemplate =
        {
            (CategoryKind.Income, "Salary", "الراتب", false),
            (CategoryKind.Income, "Other Income", "دخل آخر", false),
            (CategoryKind.Expense, "Food", "طعام", false),
            (CategoryKind.Expense, "Transport", "مواصلات", false),
            (CategoryKind.Expense, "Other Expense", "مصاريف أخرى", false),
            (CategoryKind.Expense, "Zakat/Charity", "زكاة/صدقة", true),
        };

        private IRateService RateService { get; }

        public AccountService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization, IRateService rateService)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
        }

        #region Public Methods

        public async Task<ResponseDto<AccountDto>> GetByUser(string? userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<AccountDto> response = new ResponseDto<AccountDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountAsync(account.Id);

            AccountDto dto = Mapper.Map<AccountDto>(account);
            dto.TotalBalance = await ComputeTotalBalanceAsync(account, wallets, cancellationToken);

            return response.GetSuccessResponse(dto);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Create(string? userId, CreateAccountDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            int id = ParseUserId(userId);

            if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseCurrencyCode))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (await UnitOfWork.AccountRepository.AnyAsync(a => a.UserId == id))
                throw new InvalidRequestException(Localization.AccountAlreadyExists);

            Currency baseCurrency = await UnitOfWork.CurrencyRepository.FirstOrDefaultAsync(c => c.Code == request.BaseCurrencyCode)
                ?? throw new InvalidRequestException(Localization.InvalidBaseCurrency);

            Language accountLanguage = string.Equals(CultureInfo.CurrentCulture.Name, "ar", StringComparison.OrdinalIgnoreCase)
                ? Language.Ar
                : Language.En;

            Account account = new()
            {
                UserId = id,
                Name = request.Name,
                BaseCurrencyCode = baseCurrency.Code,
                DisplayCalendar = DisplayCalendar.Hijri,
                Theme = Theme.Dark,
                Language = accountLanguage,
            };
            UnitOfWork.AccountRepository.Create(account);

            WalletType cashWalletType = await UnitOfWork.WalletTypeRepository.FirstOrDefaultAsync(wt => wt.EnName == CashWalletTypeName)
                ?? throw new ObjectNotFoundException(Localization.GeneralError);

            string cashWalletCurrencyCode = baseCurrency.Type == CurrencyType.Fiat ? baseCurrency.Code : FallbackFiatCurrencyCode;

            Wallet cashWallet = new()
            {
                Account = account,
                WalletType = cashWalletType,
                Name = cashWalletType.EnName,
                CurrencyCode = cashWalletCurrencyCode,
                InitialAmount = request.InitialAmount ?? 0m,
                ExcludeFromTotal = false,
                IsArchived = false,
            };
            UnitOfWork.WalletRepository.Create(cashWallet);

            foreach ((CategoryKind kind, string enName, string arName, bool isProtected) in DefaultCategoryTemplate)
            {
                UnitOfWork.CategoryRepository.Create(new Category
                {
                    Account = account,
                    Kind = kind,
                    Name = accountLanguage == Language.Ar ? arName : enName,
                    IsProtected = isProtected,
                });
            }

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.AccountCreated)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> UpdateSettings(string? userId, AccountSettingsDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            if (!string.IsNullOrWhiteSpace(request.BaseCurrencyCode))
            {
                if (!await UnitOfWork.CurrencyRepository.AnyAsync(c => c.Code == request.BaseCurrencyCode))
                    throw new InvalidRequestException(Localization.InvalidBaseCurrency);
                account.BaseCurrencyCode = request.BaseCurrencyCode;
            }

            if (Enum.TryParse(request.DisplayCalendar, true, out DisplayCalendar displayCalendar))
                account.DisplayCalendar = displayCalendar;

            if (Enum.TryParse(request.Theme, true, out Theme theme))
                account.Theme = theme;

            if (Enum.TryParse(request.Language, true, out Language language))
                account.Language = language;

            UnitOfWork.AccountRepository.Update(account);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion

        #region Private Methods

        private int ParseUserId(string? userId)
        {
            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);
            return id;
        }

        private async Task<decimal> ComputeTotalBalanceAsync(Account account, List<Wallet> wallets, CancellationToken cancellationToken)
        {
            decimal total = 0m;
            foreach (Wallet wallet in wallets.Where(w => !w.ExcludeFromTotal))
            {
                decimal balance = await GetWalletBalanceAsync(wallet);
                decimal rate = string.Equals(wallet.CurrencyCode, account.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)
                    ? 1m
                    : await RateService.GetLatestAsync(wallet.CurrencyCode, account.BaseCurrencyCode, cancellationToken);
                total += balance * rate;
            }
            return total;
        }

        #endregion
    }
}
