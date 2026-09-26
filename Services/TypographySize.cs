using System;

namespace DDay3.Services
{
    // Stored values are reference points, independent for each role. Window scaling is separate.
    internal static class TypographySize
    {
        internal static string DefaultFamily(string key)
        {
            if (key == "time") return "Consolas";
            return key == "dday_count" || key == "calendar" ? "Tahoma" : "Malgun Gothic";
        }
        internal static int DefaultPoints(string key)
        {
            switch (key)
            {
                case "time": return 45;
                case "date": case "dday_title": return 12;
                case "dday_count": case "calendar": return 15;
                case "dday_date": return 10;
                default: throw new ArgumentException("Unknown text role", "key");
            }
        }
        internal static bool TryNormalize(double value, out int points)
        {
            points = 0;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 5 || value > 150) return false;
            points = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return true;
        }
        internal static int Step(int value, int direction)
        {
            int normalized;
            if (!TryNormalize(value, out normalized)) normalized = 12;
            return Math.Max(5, Math.Min(150, normalized + Math.Sign(direction)));
        }
    }
}
