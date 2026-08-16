using Common.GoldPurity;

namespace Meezan.Tests.GoldPurity
{
    // meezan-backend.md §7.1 / spec §3.2's karat purity table, reproduced against the spec's own
    // worked example (10g at each of the five supported karats) — including the 3-decimal
    // rounding every PureGoldGrams value is subject to once persisted (the decimal(18,3) column;
    // ToPureGoldGrams itself never rounds, matching this codebase's convention of always leaving
    // rounding to the storage precision rather than doing it in C#).
    public class GoldPurityCalculatorTests
    {
        private readonly IGoldPurityCalculator _calculator = new GoldPurityCalculator();

        [Theory]
        [InlineData(24, "10.000")]
        [InlineData(22, "9.167")]
        [InlineData(21, "8.750")]
        [InlineData(18, "7.500")]
        [InlineData(14, "5.833")]
        public void ToPureGoldGrams_MatchesSpecWorkedExample_WhenRoundedToStoragePrecision(int karat, string expectedRounded)
        {
            decimal raw = _calculator.ToPureGoldGrams(10m, karat);
            decimal rounded = Math.Round(raw, 3, MidpointRounding.AwayFromZero);

            Assert.Equal(decimal.Parse(expectedRounded), rounded);
        }

        [Fact]
        public void ToPureGoldGrams_Returns24KaratUnchanged()
        {
            // 24K is (near enough) pure — grams in, identical grams out, no rounding involved.
            Assert.Equal(37.5m, _calculator.ToPureGoldGrams(37.5m, 24));
        }

        [Fact]
        public void ToPureGoldGrams_ReturnsZero_WhenAmountIsZero()
        {
            Assert.Equal(0m, _calculator.ToPureGoldGrams(0m, 18));
        }

        [Theory]
        [InlineData(24)]
        [InlineData(22)]
        [InlineData(21)]
        [InlineData(18)]
        [InlineData(14)]
        public void IsValidKarat_AcceptsAllFiveSupportedKarats(int karat)
        {
            Assert.True(_calculator.IsValidKarat(karat));
        }

        [Theory]
        [InlineData(23)]
        [InlineData(20)]
        [InlineData(10)]
        [InlineData(0)]
        [InlineData(-24)]
        public void IsValidKarat_RejectsEveryUnsupportedValue(int karat)
        {
            Assert.False(_calculator.IsValidKarat(karat));
        }
    }
}
