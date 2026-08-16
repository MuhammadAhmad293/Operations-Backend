namespace Meezan.Dto.DTOs.Transaction
{
    // BR-11: transaction lists are grouped by day.
    public class TransactionGroupDto
    {
        public DateOnly Date { get; set; }
        public List<TransactionDto> Transactions { get; set; } = new();
    }
}
