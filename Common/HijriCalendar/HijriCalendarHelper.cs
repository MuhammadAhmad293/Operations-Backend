using System.Globalization;

namespace Common.HijriCalendar
{
    // Wraps System.Globalization.UmAlQuraCalendar (the Saudi civil calendar, per
    // meezan-backend.md §3). Every Hijri date in this system is stored and compared as a
    // zero-padded sortable string "yyyy-MM-dd" (e.g. "1448-01-05") — never a display-formatted
    // string with localized month names — so callers can compare hawl dates with plain string
    // comparison instead of parsing.
    public class HijriCalendarHelper : IHijriCalendarHelper
    {
        private readonly UmAlQuraCalendar _calendar = new();

        public string ToHijriString(DateOnly gregorianDate)
        {
            DateTime dateTime = gregorianDate.ToDateTime(TimeOnly.MinValue);
            return Format(_calendar.GetYear(dateTime), _calendar.GetMonth(dateTime), _calendar.GetDayOfMonth(dateTime));
        }

        public DateOnly ToGregorian(string hijriDate)
        {
            (int year, int month, int day) = Parse(hijriDate);
            DateTime dateTime = _calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            return DateOnly.FromDateTime(dateTime);
        }

        public string AddHijriYears(string hijriDate, int years)
        {
            (int year, int month, int day) = Parse(hijriDate);
            DateTime dateTime = _calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            DateTime shifted = _calendar.AddYears(dateTime, years);
            return Format(_calendar.GetYear(shifted), _calendar.GetMonth(shifted), _calendar.GetDayOfMonth(shifted));
        }

        public string AddDays(string hijriDate, int days)
        {
            (int year, int month, int day) = Parse(hijriDate);
            DateTime dateTime = _calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            DateTime shifted = _calendar.AddDays(dateTime, days);
            return Format(_calendar.GetYear(shifted), _calendar.GetMonth(shifted), _calendar.GetDayOfMonth(shifted));
        }

        private static (int Year, int Month, int Day) Parse(string hijriDate)
        {
            string[] parts = hijriDate.Split('-');
            return (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        }

        private static string Format(int year, int month, int day)
            => $"{year:D4}-{month:D2}-{day:D2}";
    }
}
