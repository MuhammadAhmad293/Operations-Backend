namespace Meezan.Dto.DTOs.Statistics
{
    public class StructureCategoryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal Percent { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }
}
