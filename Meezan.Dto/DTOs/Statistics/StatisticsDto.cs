using Meezan.Dto.DTOs.Overview;

namespace Meezan.Dto.DTOs.Statistics
{
    // UC-20: opening/ending balance (BR-12 total-balance formula evaluated at the period's two
    // boundary dates, excluded wallets omitted per BR-04) + the same period's Overview block.
    public class StatisticsDto
    {
        public decimal OpeningBalance { get; set; }
        public decimal EndingBalance { get; set; }

        // BR-04: non-null only when at least one wallet is excludeFromTotal, explaining why the
        // opening/ending balance doesn't reflect every wallet.
        public string? ExcludedNote { get; set; }

        public OverviewDto Overview { get; set; }
    }
}
