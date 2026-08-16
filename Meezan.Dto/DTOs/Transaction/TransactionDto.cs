namespace Meezan.Dto.DTOs.Transaction
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public DateOnly DateGregorian { get; set; }
        public string DateHijri { get; set; }
        public TimeOnly Time { get; set; }
        public decimal Amount { get; set; }
        public int WalletId { get; set; }
        public int? ToWalletId { get; set; }
        public int? CategoryId { get; set; }
        // BR-06: resolved even when the referenced wallet/category is soft-deleted, since it
        // remains this transaction's *current* selected value — only populated by GetById
        // (the "open transaction" edit flow, UC-15), not the list/search views.
        public string? WalletName { get; set; }
        public string? ToWalletName { get; set; }
        public string? CategoryName { get; set; }
        public int? Karat { get; set; }
        public decimal? PureGoldGrams { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? ConvertedAmount { get; set; }
        public bool IsFee { get; set; }
        public int? ParentTransactionId { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public int? ZakatCycleId { get; set; }
    }
}
