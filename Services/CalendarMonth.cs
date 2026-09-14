using System;
using System.Globalization;

namespace DDay3.Services
{
    // The same starting weekday drives both the header and the fixed six-row date grid.
    internal static class CalendarMonth
    {
        internal static DateTime?[] GetDays(DateTime month, DayOfWeek firstDay = DayOfWeek.Sunday)
        {
            DateTime first = new DateTime(month.Year, month.Month, 1);
            ValidateFirstDay(firstDay);
            int offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
            DateTime?[] days = new DateTime?[42];
            long firstDayNumber = first.Ticks / TimeSpan.TicksPerDay;
            long lastDay = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;
            for (int i = 0; i < days.Length; i++)
            {
                long day = firstDayNumber - offset + i;
                if (day >= 0 && day <= lastDay) days[i] = new DateTime(day * TimeSpan.TicksPerDay);
            }
            return days;
        }

        internal static DayOfWeek[] Weekdays(DayOfWeek firstDay)
        {
            ValidateFirstDay(firstDay);
            DayOfWeek[] days = new DayOfWeek[7];
            for (int i = 0; i < days.Length; i++) days[i] = (DayOfWeek)(((int)firstDay + i) % 7);
            return days;
        }
        private static void ValidateFirstDay(DayOfWeek firstDay)
        {
            if (firstDay < DayOfWeek.Sunday || firstDay > DayOfWeek.Saturday)
                throw new ArgumentOutOfRangeException("firstDay");
        }

        internal static bool TrySelect(string yearText, int month, out DateTime selected)
        {
            selected = default(DateTime);
            int year;
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out year) ||
                year < 1 || year > 9999 || month < 1 || month > 12) return false;
            selected = new DateTime(year, month, 1);
            return true;
        }

        internal static DateTime MoveMonth(DateTime month, int delta)
        {
            long index = (month.Year - 1L) * 12 + month.Month - 1 + delta;
            index = Math.Max(0, Math.Min(9999L * 12 - 1, index));
            return new DateTime((int)(index / 12) + 1, (int)(index % 12) + 1, 1);
        }
    }
}
