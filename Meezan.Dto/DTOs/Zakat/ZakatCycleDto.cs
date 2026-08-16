namespace Meezan.Dto.DTOs.Zakat
{
    // GetCycles' full-history row shape. Due/Remaining figures are null for Active/Broken cycles
    // (nothing due yet to be null-relative-to) — populated once a cycle has ever gone Due.
    public class ZakatCycleDto
    {
        public int Id { get; set; }
        public string HawlStartHijri { get; set; }
        public string HawlDueHijri { get; set; }
        public string Status { get; set; }
        public decimal? DueGoldGrams { get; set; }
        public decimal PaidViaExpensesGoldGrams { get; set; }
        public decimal ExternalPaidGoldGrams { get; set; }
        public decimal? RemainingGoldGrams { get; set; }

        // Round-2 refinement: every gram figure also converted to the account's current base
        // currency at the current gold price, computed fresh at read time — display-only, never
        // stored, never affects Status.
        public decimal? DueBaseCurrencyValue { get; set; }
        public decimal? RemainingBaseCurrencyValue { get; set; }
    }
}
