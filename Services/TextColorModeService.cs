using System;
using System.Globalization;
using DDay3.Models;

namespace DDay3.Services
{
    // Resolve display colors from the stored palette. Never overwrite the user's originals.
    internal static class TextColorModeService
    {
        internal static string CalendarDayColor(DayOfWeek weekday, string foreground, bool distinguishWeekends)
        {
            if (!distinguishWeekends || (weekday != DayOfWeek.Sunday && weekday != DayOfWeek.Saturday)) return foreground;
            int rgb;
            bool light = foreground != null && foreground.Length == 7 &&
                int.TryParse(foreground.Substring(1), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out rgb) &&
                (((rgb >> 16) & 255) * 0.299 + ((rgb >> 8) & 255) * 0.587 + (rgb & 255) * 0.114) >= 128;
            return weekday == DayOfWeek.Sunday ? (light ? "#FFACAC" : "#8F2030") : (light ? "#A9CCFF" : "#23548E");
        }

        internal static AppSettings Resolve(AppSettings source)
        {
            AppSettings value = source.Clone();
            value.ColorTime = Color(source.ColorTime, source.TextColorMode);
            value.ColorDate = Color(source.ColorDate, source.TextColorMode);
            value.ColorDDayTitle = Color(source.ColorDDayTitle, source.TextColorMode);
            value.ColorDDayCount = Color(source.ColorDDayCount, source.TextColorMode);
            value.ColorDDayDate = Color(source.ColorDDayDate, source.TextColorMode);
            value.ColorCalendar = Color(source.ColorCalendar, source.TextColorMode);
            return value;
        }

        internal static string Color(string original, string mode)
        {
            if (mode != "light" && mode != "dark") return original;
            int rgb;
            if (original == null || original.Length != 7 || original[0] != '#' ||
                !int.TryParse(original.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                return mode == "dark" ? "#404040" : "#FFFFFF";
            double r = rgb >> 16 & 255, g = rgb >> 8 & 255, b = rgb & 255;
            double lightness = (Math.Max(r, Math.Max(g, b)) + Math.Min(r, Math.Min(g, b))) / 510.0;
            if (mode == "dark" && lightness > 0.25)
            {
                double ratio = 0.25 / lightness;
                r *= ratio; g *= ratio; b *= ratio;
            }
            else if (mode == "light" && lightness < 0.78)
            {
                double ratio = (0.78 - lightness) / (1 - lightness);
                r += (255 - r) * ratio; g += (255 - g) * ratio; b += (255 - b) * ratio;
            }
            return "#" + ((int)Math.Round(r)).ToString("X2") + ((int)Math.Round(g)).ToString("X2") + ((int)Math.Round(b)).ToString("X2");
        }
    }
}
