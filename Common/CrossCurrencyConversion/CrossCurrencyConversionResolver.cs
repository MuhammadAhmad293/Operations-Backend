namespace Common.CrossCurrencyConversion
{
    // BR-10: for a cross-currency transfer, the user may override either the final rate or the
    // converted amount directly; if they override the amount, the "final rate used" is derived
    // from it for audit purposes. Same-currency transfers (or non-transfers) get neither field.
    // Returns (null, null) when a live rate lookup is still needed by the caller (no override
    // supplied) — resolving that is async and stays the caller's responsibility.
    public class CrossCurrencyConversionResolver : ICrossCurrencyConversionResolver
    {
        public (decimal? ExchangeRate, decimal? ConvertedAmount) Resolve(
            bool isCrossCurrencyTransfer, decimal amount, decimal? requestedRate, decimal? requestedConvertedAmount)
        {
            if (!isCrossCurrencyTransfer)
                return (null, null);

            if (requestedConvertedAmount.HasValue)
                return (amount != 0 ? requestedConvertedAmount.Value / amount : null, requestedConvertedAmount.Value);

            if (requestedRate.HasValue)
                return (requestedRate.Value, amount * requestedRate.Value);

            return (null, null);
        }
    }
}
