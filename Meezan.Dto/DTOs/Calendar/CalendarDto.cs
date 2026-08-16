namespace Meezan.Dto.DTOs.Calendar
{
    // UC-19: monthly grid — one entry per calendar day in the month (zero-valued for days with
    // no activity, so the frontend can render every cell), plus month-wide totals for the header.
    public class CalendarDto
    {
        public List<CalendarDayDto> Days { get; set; } = new();
        public decimal MonthIncome { get; set; }
        public decimal MonthExpense { get; set; }
        public decimal MonthTotal { get; set; }
    }
}
