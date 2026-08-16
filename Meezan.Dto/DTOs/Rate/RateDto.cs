namespace Meezan.Dto.DTOs.Rate
{
    public class RateDto
    {
        public string QuoteCurrencyCode { get; set; }
        public decimal Rate { get; set; }

        // BR-19: "on fetch failure, the last snapshot remains in use, displayed as 'rates as of
        // {fetchedAt}'" — always populated (not just on failure), so the frontend can render the
        // caption for every rate shown. Same-currency (1:1) rates use the current time (exact by
        // definition, never snapshot-derived); a cross-through-USD rate uses the older of its two
        // legs' FetchedAt, since that's the binding freshness constraint on the computed rate.
        public DateTime FetchedAt { get; set; }
    }
}
