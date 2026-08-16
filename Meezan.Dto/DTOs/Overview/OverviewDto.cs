namespace Meezan.Dto.DTOs.Overview
{
    // BR-11: Total = Income - Expense for the selected period. All wallet-currency amounts are
    // converted to the account's base currency at the latest rate (same convention as the
    // account-total balance) so a single figure is meaningful across multi-currency wallets.
    public class OverviewDto
    {
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Total { get; set; }
    }
}
