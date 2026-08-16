namespace Meezan.Dto.DTOs.Wallet
{
    public class CreateWalletDto
    {
        public string Name { get; set; }
        public int WalletTypeId { get; set; }
        public string CurrencyCode { get; set; }
        public decimal? InitialAmount { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool ExcludeFromTotal { get; set; }
    }
}
