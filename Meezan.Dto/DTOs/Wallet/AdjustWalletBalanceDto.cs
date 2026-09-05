namespace Meezan.Dto.DTOs.Wallet
{
    public class AdjustWalletBalanceDto
    {
        public int Id { get; set; }
        public decimal NewBalance { get; set; }
        public string? Note { get; set; }
    }
}
