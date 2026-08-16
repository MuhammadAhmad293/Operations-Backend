namespace Common.GoldPurity
{
    // BR-03's karat purity table: pureGrams = grams × (karat ÷ 24). Only these five karats are
    // supported anywhere in the system — 24 (≈0.999 pure), 22, 21, 18, 14.
    public class GoldPurityCalculator : IGoldPurityCalculator
    {
        public static readonly int[] ValidKarats = { 24, 22, 21, 18, 14 };

        public bool IsValidKarat(int karat) => Array.IndexOf(ValidKarats, karat) >= 0;

        public decimal ToPureGoldGrams(decimal amount, int karat) => amount * karat / 24m;
    }
}
