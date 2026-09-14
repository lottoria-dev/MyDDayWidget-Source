using System;
using System.Globalization;
namespace DDay3.Services
{
    internal static class DateDisplayFormat
    {
        internal static string Format(DateTime date, string format) { return FormatParts(date.Year, date.Month, date.Day, format); }
        // Lunar month/day components must not be constructed as a Gregorian DateTime.
        internal static string FormatParts(int year, int month, int day, string format)
        {
            string y = year.ToString("0000", CultureInfo.InvariantCulture);
            string m = month.ToString("00", CultureInfo.InvariantCulture);
            string d = day.ToString("00", CultureInfo.InvariantCulture);
            if (format == "mm/dd/yyyy") return m + "/" + d + "/" + y;
            if (format == "dd/mm/yyyy") return d + "/" + m + "/" + y;
            return y + "-" + m + "-" + d;
        }
    }
}
