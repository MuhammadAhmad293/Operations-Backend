namespace Common.CrossCurrencyConversion
{
    public interface ICrossCurrencyConversionResolver
    {
        (decimal? ExchangeRate, decimal? ConvertedAmount) Resolve(
            bool isCrossCurrencyTransfer, decimal amount, decimal? requestedRate, decimal? requestedConvertedAmount);
    }
}
