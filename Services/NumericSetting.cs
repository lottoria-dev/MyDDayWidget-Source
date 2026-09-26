using System;
using System.Globalization;

namespace DDay3.Services
{
    internal static class NumericSetting
    {
        internal static double Normalize(double value, double minimum, double maximum, bool wrap)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return minimum;
            value = Math.Round(value, MidpointRounding.AwayFromZero);
            if (wrap && maximum >= minimum)
            {
                double length = maximum - minimum + 1;
                return ((value - minimum) % length + length) % length + minimum;
            }
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        internal static bool TryParse(string text, double minimum, double maximum, bool wrap, out double value)
        {
            int number;
            value = minimum;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
            value = Normalize(number, minimum, maximum, wrap);
            return true;
        }
    }
}
