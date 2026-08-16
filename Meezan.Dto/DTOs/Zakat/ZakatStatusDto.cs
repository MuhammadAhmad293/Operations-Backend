namespace Meezan.Dto.DTOs.Zakat
{
    // UC-21: the Zakat screen's dashboard view.
    public class ZakatStatusDto
    {
        public decimal PotGoldGrams { get; set; }
        public decimal PotBaseCurrencyValue { get; set; }
        public bool NisabReached { get; set; }

        // UC-21 Alternate A1: "The total has not reached the nisab" — populated only when
        // nisab isn't reached AND no cycle is running (i.e. genuinely nothing to show yet).
        public string? Message { get; set; }

        public ZakatActiveCycleDto? ActiveCycle { get; set; }

        // Σ RemainingGoldGrams across every Due cycle — the account may owe more than one
        // year's zakat at once (Live Spec Correction: multiple simultaneous Due cycles).
        public decimal TotalOutstandingGoldGrams { get; set; }
        public decimal TotalOutstandingBaseCurrencyValue { get; set; }
    }
}
