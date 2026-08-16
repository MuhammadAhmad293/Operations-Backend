namespace Common.HijriCalendar
{
    public interface IHijriCalendarHelper
    {
        string ToHijriString(DateOnly gregorianDate);
        DateOnly ToGregorian(string hijriDate);
        string AddHijriYears(string hijriDate, int years);
        string AddDays(string hijriDate, int days);
    }
}
