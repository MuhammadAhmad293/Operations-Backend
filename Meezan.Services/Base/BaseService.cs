using Meezan.DataModel.Entities;
using Meezan.IRepositories.UnitOfWork;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.Base
{
    public class BaseService
    {
        protected IUnitOfWork UnitOfWork { get; }
        protected IMapper Mapper { get; }
        public ILocalizationService Localization { get; }
        public BaseService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            UnitOfWork = unitOfWork;
            Mapper = mapper;
            Localization = localizationService;
        }

        // Shared balance computation (meezan-backend.md §3): InitialAmount + the net signed effect
        // of every transaction touching this wallet, computed on read rather than cached. Used by
        // Wallet/Account today; Zakat (Phase 012) reuses the same method. asOfDateInclusive lets
        // StatisticsService (Phase 011) compute a *past* balance (opening/ending, at the period's
        // boundary dates) — omitted, it's today's balance exactly as before.
        protected async Task<decimal> GetWalletBalanceAsync(Wallet wallet, DateOnly? asOfDateInclusive = null)
            => wallet.InitialAmount + await UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id, asOfDateInclusive);

        // Every domain service scopes its work to the caller's own account — resolve it once here
        // rather than repeating the parse-userId-then-fetch-or-404 pattern in each service.
        protected async Task<Account> GetAccountByUserIdAsync(string? userId)
        {
            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);

            return await UnitOfWork.AccountRepository.FirstOrDefaultAsync(a => a.UserId == id)
                ?? throw new ObjectNotFoundException(Localization.AccountNotFound);
        }

        // BR-11 period filters, resolved against the current Gregorian date. "custom" (or no
        // recognized period) falls back to the caller's explicit from/to. Originally
        // TransactionService-only (Phase 009); moved here in Phase 011 since Overview/Statistics
        // need the identical period-to-date-range resolution.
        protected static (DateOnly? From, DateOnly? To) ResolvePeriod(string? period, DateOnly? from, DateOnly? to)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            return period?.ToLowerInvariant() switch
            {
                "daily" => (today, today),
                "weekly" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(6 - (int)today.DayOfWeek)),
                "monthly" => (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
                "quarterly" => ResolveQuarter(today),
                "yearly" => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
                "all" => (null, null),
                _ => (from, to),
            };
        }

        private static (DateOnly, DateOnly) ResolveQuarter(DateOnly today)
        {
            int quarterStartMonth = ((today.Month - 1) / 3 * 3) + 1;
            DateOnly start = new(today.Year, quarterStartMonth, 1);
            DateOnly end = start.AddMonths(3).AddDays(-1);
            return (start, end);
        }
    }
}
