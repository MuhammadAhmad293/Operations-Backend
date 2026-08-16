namespace Common.GoldPurity
{
    public interface IGoldPurityCalculator
    {
        bool IsValidKarat(int karat);
        decimal ToPureGoldGrams(decimal amount, int karat);
    }
}
