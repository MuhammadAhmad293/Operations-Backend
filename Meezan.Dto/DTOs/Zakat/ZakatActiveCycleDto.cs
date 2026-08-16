namespace Meezan.Dto.DTOs.Zakat
{
    // GetStatus's "current hawl in progress" section. ProjectedDueGoldGrams is *not* the stored
    // ZakatDueGoldGrams (that's null until the cycle actually goes Due) — it's today's pot × 2.5%,
    // computed fresh for display only, showing what would be due if the hawl completed right now.
    public class ZakatActiveCycleDto
    {
        public string HawlStartHijri { get; set; }
        public string HawlDueHijri { get; set; }
        public decimal ProjectedDueGoldGrams { get; set; }
        public decimal ProjectedDueBaseCurrencyValue { get; set; }
    }
}
