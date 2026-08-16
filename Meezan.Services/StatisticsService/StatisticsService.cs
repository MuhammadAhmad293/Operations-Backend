using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Overview;
using Meezan.Dto.DTOs.Statistics;
using Meezan.IRepositories.IRepository;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.StatisticsService
{
    public class StatisticsService : BaseService, IStatisticsService
    {
        private IRateService RateService { get; }
        private IOverviewService OverviewService { get; }

        public StatisticsService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IRateService rateService, IOverviewService overviewService)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
            OverviewService = overviewService;
        }

        // UC-20: opening/ending balance is BR-12's total-balance formula (non-excluded wallets,
        // converted to base currency) evaluated at the period's two boundary dates, plus the same
        // period's Overview block (composed via IOverviewService, not recomputed here).
        public async Task<ResponseDto<StatisticsDto>> GetStatistics(string? userId, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            ResponseDto<StatisticsDto> response = new ResponseDto<StatisticsDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            (DateOnly? periodFrom, DateOnly? periodTo) = ResolvePeriod(period, from, to);

            List<Wallet> allWallets = await UnitOfWork.WalletRepository.GetByAccountAsync(account.Id);
            List<Wallet> includedWallets = allWallets.Where(w => !w.ExcludeFromTotal).ToList();
            bool hasExcludedWallets = includedWallets.Count != allWallets.Count;

            BaseCurrencyConverter converter = new(account, includedWallets, RateService);

            // "Opening" = balance the instant before the period starts (no lower bound for "all"
            // — DateOnly.MinValue can never match a real transaction, so this naturally reduces
            // to each wallet's InitialAmount only, exactly what "opening balance of all time" means).
            DateOnly openingCutoff = periodFrom.HasValue ? periodFrom.Value.AddDays(-1) : DateOnly.MinValue;
            decimal openingBalance = await ComputeTotalBalanceAsync(includedWallets, openingCutoff, converter, cancellationToken);

            // "Ending" = balance as of the period's end; null (unbounded "all"/current) falls
            // through to GetWalletBalanceAsync's default (today's real-time balance).
            decimal endingBalance = await ComputeTotalBalanceAsync(includedWallets, periodTo, converter, cancellationToken);

            ResponseDto<OverviewDto> overviewResponse = await OverviewService.GetOverview(userId, period, from, to, cancellationToken);

            StatisticsDto statistics = new()
            {
                OpeningBalance = openingBalance,
                EndingBalance = endingBalance,
                ExcludedNote = hasExcludedWallets ? Localization.StatisticsExcludedWalletsNote : null,
                Overview = overviewResponse.Data,
            };

            return response.GetSuccessResponse(statistics);
        }

        // UC-20 Structure: per-category {percent, amount, txCount} donut data for one kind
        // (Income or Expense) tab. Percent uses the largest-remainder rounding method so the
        // tab's percentages sum to exactly 100.00, per the UC's explicit acceptance criterion.
        public async Task<ResponseDto<StructureDto>> GetStructure(string? userId, string kind, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            ResponseDto<StructureDto> response = new ResponseDto<StructureDto>().GetErrorResponse();

            if (!Enum.TryParse(kind, true, out CategoryKind parsedKind))
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            (DateOnly? periodFrom, DateOnly? periodTo) = ResolvePeriod(period, from, to);

            TransactionType transactionType = parsedKind == CategoryKind.Income ? TransactionType.Income : TransactionType.Expense;
            TransactionFilter filter = new() { From = periodFrom, To = periodTo, Type = transactionType };
            List<Transaction> transactions = await UnitOfWork.TransactionRepository.GetFilteredAsync(account.Id, filter);

            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountIncludingDeletedAsync(account.Id);
            BaseCurrencyConverter converter = new(account, wallets, RateService);

            List<Category> categories = await UnitOfWork.CategoryRepository.GetByKindIncludingDeletedAsync(account.Id, parsedKind);
            Dictionary<int, Category> categoriesById = categories.ToDictionary(c => c.Id);

            Dictionary<int, (decimal Amount, int Count)> byCategory = new();
            foreach (Transaction transaction in transactions)
            {
                decimal converted = await converter.ConvertAsync(transaction, cancellationToken);
                int categoryId = transaction.CategoryId!.Value; // BR-07: mandatory for Income/Expense

                (decimal Amount, int Count) current = byCategory.TryGetValue(categoryId, out (decimal Amount, int Count) existing) ? existing : (0m, 0);
                byCategory[categoryId] = (current.Amount + converted, current.Count + 1);
            }

            List<StructureCategoryDto> categoryDtos = BuildCategoryDtos(byCategory, categoriesById);

            return response.GetSuccessResponse(new StructureDto { Kind = parsedKind.ToString(), Categories = categoryDtos });
        }

        private async Task<decimal> ComputeTotalBalanceAsync(List<Wallet> wallets, DateOnly? asOfDateInclusive, BaseCurrencyConverter converter, CancellationToken cancellationToken)
        {
            decimal total = 0m;
            foreach (Wallet wallet in wallets)
            {
                decimal balance = await GetWalletBalanceAsync(wallet, asOfDateInclusive);
                total += await converter.ConvertAmountAsync(balance, wallet.CurrencyCode, cancellationToken);
            }
            return total;
        }

        // Largest-remainder rounding: each category's raw percent is floored to 2 decimals (so
        // the rounded sum never exceeds 100), then the shortfall is handed out as 0.01 increments
        // to the categories with the largest fractional remainder — guaranteed to land on exactly
        // 100.00 since flooring always loses less than 0.01 per category.
        private static List<StructureCategoryDto> BuildCategoryDtos(Dictionary<int, (decimal Amount, int Count)> byCategory, Dictionary<int, Category> categoriesById)
        {
            decimal grandTotal = byCategory.Values.Sum(v => v.Amount);
            if (grandTotal <= 0m)
                return new List<StructureCategoryDto>();

            List<(int CategoryId, decimal Amount, int Count, decimal FlooredPercent, decimal Remainder)> entries = byCategory
                .Select(kv =>
                {
                    decimal rawPercent = kv.Value.Amount / grandTotal * 100m;
                    decimal flooredPercent = Math.Floor(rawPercent * 100m) / 100m;
                    return (kv.Key, kv.Value.Amount, kv.Value.Count, flooredPercent, rawPercent - flooredPercent);
                })
                .ToList();

            int centsShortfall = (int)Math.Round((100m - entries.Sum(e => e.FlooredPercent)) * 100m);
            List<int> byLargestRemainder = entries.OrderByDescending(e => e.Remainder).Select(e => e.CategoryId).ToList();

            Dictionary<int, decimal> finalPercents = entries.ToDictionary(e => e.CategoryId, e => e.FlooredPercent);
            for (int i = 0; i < centsShortfall && i < byLargestRemainder.Count; i++)
                finalPercents[byLargestRemainder[i]] += 0.01m;

            return entries
                .Select(e => new StructureCategoryDto
                {
                    CategoryId = e.CategoryId,
                    CategoryName = categoriesById[e.CategoryId].Name,
                    Percent = finalPercents[e.CategoryId],
                    Amount = e.Amount,
                    TransactionCount = e.Count,
                })
                .OrderByDescending(d => d.Amount)
                .ToList();
        }
    }
}
