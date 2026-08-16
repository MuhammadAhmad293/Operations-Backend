namespace Meezan.Dto.DTOs.Transaction
{
    public class CreateTransactionDto
    {
        public string Type { get; set; }
        public DateOnly DateGregorian { get; set; }
        public TimeOnly Time { get; set; }
        public decimal Amount { get; set; }
        public int WalletId { get; set; }
        public int? ToWalletId { get; set; }
        public int? CategoryId { get; set; }
        public int? Karat { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? ConvertedAmount { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public CreateFeeDto? Fee { get; set; }
    }
}
