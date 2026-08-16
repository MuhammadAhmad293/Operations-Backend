using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Overview;
using Meezan.IRepositories.IRepository;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.OverviewService
{
    public class OverviewService : BaseService, IOverviewService
    {
        private IRateService RateService { get; }

        public OverviewService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization, IRateService rateService)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
        }

        public async Task<ResponseDto<OverviewDto>> GetOverview(string? userId, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            ResponseDto<OverviewDto> response = new ResponseDto<OverviewDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            (DateOnly? periodFrom, DateOnly? periodTo) = ResolvePeriod(period, from, to);

            OverviewDto overview = await ComputeOverviewAsync(account, periodFrom, periodTo, cancellationToken);

            return response.GetSuccessResponse(overview);
        }

        // BR-11: Income/Expense-typed transactions only (Transfers move money between the
        // account's own wallets — no external flow — so they're excluded); every wallet counts
        // regardless of ExcludeFromTotal (that flag is scoped to balance snapshots per BR-04,
        // not transaction-flow views). Amounts convert to the account's base currency via
        // BaseCurrencyConverter (shared with CalendarService/StatisticsService).
        internal async Task<OverviewDto> ComputeOverviewAsync(Account account, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
        {
            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountIncludingDeletedAsync(account.Id);
            BaseCurrencyConverter converter = new(account, wallets, RateService);

            TransactionFilter filter = new() { From = from, To = to };
            List<Transaction> transactions = await UnitOfWork.TransactionRepository.GetFilteredAsync(account.Id, filter);

            decimal income = 0m;
            decimal expense = 0m;
            foreach (Transaction transaction in transactions)
            {
                if (transaction.Type != TransactionType.Income && transaction.Type != TransactionType.Expense)
                    continue;

                decimal converted = await converter.ConvertAsync(transaction, cancellationToken);

                if (transaction.Type == TransactionType.Income)
                    income += converted;
                else
                    expense += converted;
            }

            return new OverviewDto { Income = income, Expense = expense, Total = income - expense };
        }
    }
}
