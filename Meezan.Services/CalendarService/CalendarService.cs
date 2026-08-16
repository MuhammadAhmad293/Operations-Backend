using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Calendar;
using Meezan.IRepositories.IRepository;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.CalendarService
{
    public class CalendarService : BaseService, ICalendarService
    {
        private IRateService RateService { get; }

        public CalendarService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization, IRateService rateService)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
        }

        // UC-19: one entry per calendar day (zero-filled when a day has no activity), plus
        // month-wide totals. Same Income/Expense-only, Transfer-excluded, base-currency-converted
        // math as OverviewService, just grouped by day instead of summed across the whole range.
        public async Task<ResponseDto<CalendarDto>> GetMonth(string? userId, int year, int month, CancellationToken cancellationToken = default)
        {
            ResponseDto<CalendarDto> response = new ResponseDto<CalendarDto>().GetErrorResponse();

            if (month < 1 || month > 12)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            DateOnly monthStart = new(year, month, 1);
            DateOnly monthEnd = new(year, month, DateTime.DaysInMonth(year, month));

            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountIncludingDeletedAsync(account.Id);
            BaseCurrencyConverter converter = new(account, wallets, RateService);

            TransactionFilter filter = new() { From = monthStart, To = monthEnd };
            List<Transaction> transactions = await UnitOfWork.TransactionRepository.GetFilteredAsync(account.Id, filter);

            Dictionary<DateOnly, (decimal Income, decimal Expense)> byDay = new();
            foreach (Transaction transaction in transactions)
            {
                if (transaction.Type != TransactionType.Income && transaction.Type != TransactionType.Expense)
                    continue;

                decimal converted = await converter.ConvertAsync(transaction, cancellationToken);
                (decimal Income, decimal Expense) current = byDay.TryGetValue(transaction.DateGregorian, out (decimal Income, decimal Expense) existing) ? existing : (0m, 0m);

                byDay[transaction.DateGregorian] = transaction.Type == TransactionType.Income
                    ? (current.Income + converted, current.Expense)
                    : (current.Income, current.Expense + converted);
            }

            List<CalendarDayDto> days = new();
            decimal monthIncome = 0m;
            decimal monthExpense = 0m;
            for (DateOnly date = monthStart; date <= monthEnd; date = date.AddDays(1))
            {
                (decimal Income, decimal Expense) dayTotals = byDay.TryGetValue(date, out (decimal Income, decimal Expense) found) ? found : (0m, 0m);

                days.Add(new CalendarDayDto
                {
                    Date = date,
                    Income = dayTotals.Income,
                    Expense = dayTotals.Expense,
                    Total = dayTotals.Income - dayTotals.Expense,
                });

                monthIncome += dayTotals.Income;
                monthExpense += dayTotals.Expense;
            }

            CalendarDto calendar = new()
            {
                Days = days,
                MonthIncome = monthIncome,
                MonthExpense = monthExpense,
                MonthTotal = monthIncome - monthExpense,
            };

            return response.GetSuccessResponse(calendar);
        }
    }
}
