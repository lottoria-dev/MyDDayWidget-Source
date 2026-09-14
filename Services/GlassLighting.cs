using System;

namespace DDay3.Services
{
    // Screen coordinates: zero is above, angles increase clockwise.
    internal static class GlassLighting
    {
        internal static int NormalizeDepth(int value) { return Math.Max(-100, Math.Min(100, value)); }
        internal static double ReliefAmount(int value) { return Math.Abs(NormalizeDepth(value)) / 100.0; }
        // Concave surfaces face the light on the opposite shoulder. The light itself never moves.
        internal static double HighlightDirection(double angle, int depth)
        { return NormalizeDirection(angle + (depth < 0 ? 180 : 0)); }

        internal static double NormalizeDirection(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return 315;
            return (Math.Round(degrees) % 360 + 360) % 360;
        }
        internal static double X(double degrees) { return Math.Sin(NormalizeDirection(degrees) * Math.PI / 180); }
        internal static double Y(double degrees) { return -Math.Cos(NormalizeDirection(degrees) * Math.PI / 180); }
        internal static double FromPoint(double x, double y) { return NormalizeDirection(Math.Atan2(x, -y) * 180 / Math.PI); }
    }
}
