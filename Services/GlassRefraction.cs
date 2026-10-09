using System;
using System.Globalization;

namespace DDay3.Services
{
    // Color and gain live in the existing attached lip, independently of the face tint.
    internal static class GlassRefraction
    {
        internal const string DefaultColor = "#7EC1EE";
        internal static string NormalizeMode(string mode)
        { return mode == "off" || mode == "clear" ? mode : "soft"; }
        internal static double Strength(string mode, double weight)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight)) weight = 0;
            return (mode == "off" ? 0 : mode == "clear" ? 1 : .3) * Math.Max(0, Math.Min(1, weight));
        }
        internal static double Gain(double strength)
        { return 1 + .9 * Math.Max(0, Math.Min(1, strength)); }
        internal static double ColorWeight(double strength)
        { return .95 * Math.Max(0, Math.Min(1, strength)); }
        internal static double MaximumWashSpan(double scale, double shorterSide)
        { return Math.Max(0, Math.Min(36 * Math.Max(.3, scale), .28 * shorterSide)); }
        internal static double WashAlpha(double distance, double incidence, int depth, double scale,
            double opacity, double strength, double shorterSide)
        {
            if (strength <= 0 || depth == 0 || opacity <= 0 || incidence <= 0 || distance < 0) return 0;
            double amount = Math.Min(2, Math.Abs(depth) / 100.0);
            double span = Math.Min(MaximumWashSpan(scale, shorterSide),
                (5 + 12 * amount + 10 * strength) * Math.Max(.3, scale));
            if (span <= 0 || distance >= span) return 0;
            double visibility = Math.Min(1, opacity / .05) * (.3 + .7 * Math.Sqrt(opacity));
            double fade = 1 - distance / span;
            fade = fade * fade * (3 - 2 * fade);
            return (16 + 36 * strength) * strength * (.4 + .6 * Math.Sqrt(Math.Min(1, amount)))
                * visibility * Math.Min(1, incidence) * fade;
        }
        internal static double WhiteMix(double distance, double scale, int depth)
        {
            double span = (1 + .028 * Math.Abs(depth)) * Math.Max(.3, scale);
            double position = Math.Max(0, Math.Min(1, distance / span));
            position = position * position * (3 - 2 * position);
            return .12 + .45 * position;
        }
        internal static string NormalizeColor(string value)
        {
            int rgb;
            return value != null && value.Length == 7 && value[0] == '#' &&
                int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)
                ? "#" + rgb.ToString("X6", CultureInfo.InvariantCulture) : DefaultColor;
        }
    }
}
