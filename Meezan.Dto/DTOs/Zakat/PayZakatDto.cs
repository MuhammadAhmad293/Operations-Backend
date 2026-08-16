namespace Meezan.Dto.DTOs.Zakat
{
    // UC-24 (revised): CycleId explicit since multiple Due cycles can coexist. Amount is
    // interpreted in the account's current base currency; omitted, it defaults to the cycle's
    // full RemainingGoldGrams converted at the current price (allows partial payment over
    // multiple calls when an explicit smaller Amount is supplied).
    public class PayZakatDto
    {
        public int CycleId { get; set; }
        public int WalletId { get; set; }
        public decimal? Amount { get; set; }
    }
}
