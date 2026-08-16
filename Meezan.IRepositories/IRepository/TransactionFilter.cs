using Meezan.DataModel.Enums;

namespace Meezan.IRepositories.IRepository
{
    // Query-layer filter for ITransactionRepository.GetFilteredAsync — not a client-facing DTO.
    public class TransactionFilter
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public int? WalletId { get; set; }
        public int? CategoryId { get; set; }
        public TransactionType? Type { get; set; }
    }
}
