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
            return (mode == "off" ? 0 : mode == "clear" ? 1 : .6) * Math.Max(0, Math.Min(1, weight));
        }
        internal static double Gain(double strength)
        { return 1 + .7 * Math.Max(0, Math.Min(1, strength)); }
        internal static double ColorWeight(double strength)
        { return .85 * Math.Max(0, Math.Min(1, strength)); }
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
