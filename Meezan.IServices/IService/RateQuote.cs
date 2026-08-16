namespace Meezan.IServices.IService
{
    // Internal model returned by every IRateProvider — the rest of the system never sees a
    // provider's raw response schema (spec §7's anti-corruption boundary).
    public class RateQuote
    {
        public string FromCurrencyCode { get; set; }
        public string ToCurrencyCode { get; set; }
        public decimal RatePerUnit { get; set; }
        public decimal? RatePerGram { get; set; }
        public DateTime FetchedAt { get; set; }
        public string Source { get; set; }
    }
}
