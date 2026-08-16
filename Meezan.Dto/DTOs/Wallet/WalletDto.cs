namespace Meezan.Dto.DTOs.Wallet
{
    public class WalletDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int WalletTypeId { get; set; }
        public string CurrencyCode { get; set; }
        public decimal Balance { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool ExcludeFromTotal { get; set; }
        public bool IsArchived { get; set; }
    }
}
