using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class Transaction : BaseEntity
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public TransactionType Type { get; set; }
        public DateOnly DateGregorian { get; set; }
        public string DateHijri { get; set; }
        public TimeOnly Time { get; set; }
        public decimal Amount { get; set; }
        public int WalletId { get; set; }
        public int? ToWalletId { get; set; }
        public int? CategoryId { get; set; }
        public int? Karat { get; set; }
        public decimal? PureGoldGrams { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? ConvertedAmount { get; set; }
        public bool IsFee { get; set; }
        public bool IsAdjustment { get; set; }
        public int? ParentTransactionId { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }

        // Zakat payment linkage (Live Spec Correction / Round-2 refinement) — the FK constraint
        // to ZakatCycle is configured in ZakatCycleConfiguration once that entity exists (sub-task 9).
        public int? ZakatCycleId { get; set; }
        public decimal? ZakatGoldGrams { get; set; }

        public Account Account { get; set; }
        public Wallet Wallet { get; set; }
        public Wallet? ToWallet { get; set; }
        public Category? Category { get; set; }
        public Transaction? ParentTransaction { get; set; }
        public ICollection<Transaction> FeeTransactions { get; set; }
    }
}
