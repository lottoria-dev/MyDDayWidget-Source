using System;
using System.Globalization;

namespace DDay3.Services
{
    internal static class LunarDateService
    {
        private static readonly KoreanLunisolarCalendar Calendar = new KoreanLunisolarCalendar();
        internal static DateTime MinimumDate { get { return Calendar.MinSupportedDateTime.Date; } }
        internal static DateTime MaximumDate { get { return Calendar.MaxSupportedDateTime.Date; } }

        internal static bool TryFormat(DateTime date, out string text, bool english = false, string dateFormat = "yyyy-mm-dd")
        {
            text = string.Empty;
            if (date.Date < MinimumDate || date.Date > MaximumDate) return false;
            int year = Calendar.GetYear(date);
            int month = Calendar.GetMonth(date);
            int leapMonth = Calendar.GetLeapMonth(year);
            bool leap = leapMonth > 0 && month == leapMonth;
            // Calendar API numbers the extra month as a thirteenth ordinal slot.
            if (leapMonth > 0 && month >= leapMonth) month--;
            text = (english ? "Lunar " : "음력 ") + DateDisplayFormat.FormatParts(year, month, Calendar.GetDayOfMonth(date), dateFormat) +
                (leap ? (english ? " (leap month)" : " (윤달)") : string.Empty);
            return true;
        }
    }
}
