namespace Meezan.Dto.DTOs.Calendar
{
    public class CalendarDayDto
    {
        public DateOnly Date { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Total { get; set; }
    }
}
