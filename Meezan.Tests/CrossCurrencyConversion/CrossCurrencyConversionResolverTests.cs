using Common.CrossCurrencyConversion;

namespace Meezan.Tests.CrossCurrencyConversion
{
    // meezan-backend.md §7.5 / BR-10 / UC-14's acceptance example: "Given USD→SAR transfer of
    // 1,000 at rate 3.75, When saved, Then from-wallet −1,000 USD And to-wallet +3,750 SAR And
    // the stored transaction carries rate 3.75."
    public class CrossCurrencyConversionResolverTests
    {
        private readonly ICrossCurrencyConversionResolver _resolver = new CrossCurrencyConversionResolver();

        [Fact]
        public void Resolve_ReturnsNothing_WhenNotACrossCurrencyTransfer()
        {
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: false, amount: 1000m, requestedRate: 3.75m, requestedConvertedAmount: 3750m);

            Assert.Null(rate);
            Assert.Null(converted);
        }

        [Fact]
        public void Resolve_ReturnsNothing_WhenCrossCurrencyButNoOverrideSupplied()
        {
            // The caller (TransactionService) is responsible for the live rate lookup in this case.
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: true, amount: 1000m, requestedRate: null, requestedConvertedAmount: null);

            Assert.Null(rate);
            Assert.Null(converted);
        }

        [Fact]
        public void Resolve_MatchesUC14sWorkedExample_WhenTheRateIsOverridden()
        {
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: true, amount: 1000m, requestedRate: 3.75m, requestedConvertedAmount: null);

            Assert.Equal(3.75m, rate);
            Assert.Equal(3750m, converted);
        }

        [Fact]
        public void Resolve_DerivesTheEffectiveRate_WhenTheConvertedAmountIsOverriddenInstead()
        {
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: true, amount: 1000m, requestedRate: null, requestedConvertedAmount: 3800m);

            Assert.Equal(3.8m, rate);
            Assert.Equal(3800m, converted);
        }

        [Fact]
        public void Resolve_PrefersTheConvertedAmountOverride_WhenBothAreSupplied()
        {
            // The converted amount is what actually lands in the destination wallet, so if the
            // caller (bug or not) supplies both, the amount — not the rate — is authoritative.
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: true, amount: 1000m, requestedRate: 3.75m, requestedConvertedAmount: 3800m);

            Assert.Equal(3.8m, rate);
            Assert.Equal(3800m, converted);
        }

        [Fact]
        public void Resolve_GuardsAgainstDivideByZero_WhenAmountIsZeroAndConvertedAmountIsOverridden()
        {
            (decimal? rate, decimal? converted) = _resolver.Resolve(isCrossCurrencyTransfer: true, amount: 0m, requestedRate: null, requestedConvertedAmount: 500m);

            Assert.Null(rate);
            Assert.Equal(500m, converted);
        }
    }
}
