namespace Meezan.Dto.DTOs.Zakat
{
    // POST /api/zakat/cycles/{id}/external-payment { amount }. CycleId comes from the route, not
    // the body. Amount is interpreted in the account's current base currency and converted to
    // pure-gold grams at the current price before being accumulated into ExternalPaidGoldGrams.
    public class PayExternalZakatDto
    {
        public decimal Amount { get; set; }
    }
}
