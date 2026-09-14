using System;
using System.Windows;
using System.Windows.Media;
using DDay3.Models;

namespace DDay3.Services
{
    internal static class LiquidGlassTheme
    {
        internal static Color ParseColor(string value, Color fallback)
        {
            try
            {
                object converted = ColorConverter.ConvertFromString(value);
                return converted is Color ? (Color)converted : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static void ApplyApplicationAccent(AppSettings settings)
        {
            Color accent = ParseColor(settings.GlassLightColor, Color.FromRgb(168, 221, 240));
            Application.Current.Resources["AccentBrush"] = Frozen(new SolidColorBrush(Darken(accent, 0.28)));
        }

        internal static Brush CreatePanelBrush(AppSettings settings)
        {
            Color accent = ParseColor(settings.GlassLightColor, Color.FromRgb(168, 221, 240));
            double strength = Math.Max(0.10, Math.Min(1.0, settings.GlassStrength));
            byte alpha = (byte)Math.Round((12 + 105 * strength) * settings.PanelOpacity);
            Color tint = Blend(accent, Colors.White, 0.68);
            return Frozen(new SolidColorBrush(Color.FromArgb(alpha, tint.R, tint.G, tint.B)));
        }

        internal static Brush CreateClockCardBrush(AppSettings settings)
        {
            Color accent = ParseColor(settings.GlassLightColor, Color.FromRgb(168, 221, 240));
            double strength = Math.Max(0.10, Math.Min(1.0, settings.GlassStrength));
            byte alpha = (byte)Math.Round((8 + 42 * strength) * settings.ClockPanelOpacity);
            Color tint = Blend(accent, Colors.White, 0.48);
            return Frozen(new SolidColorBrush(Color.FromArgb(alpha, tint.R, tint.G, tint.B)));
        }

        internal static Brush CreateScheduleCapsuleBrush(AppSettings settings)
        {
            Color accent = ParseColor(settings.GlassLightColor, Color.FromRgb(168, 221, 240));
            Color tint = Blend(accent, Colors.White, 0.58);
            double strength = Math.Max(0.10, Math.Min(1.0, settings.GlassStrength));
            byte alpha = (byte)Math.Round((18 + 34 * strength) * settings.DDayPanelOpacity);
            return Frozen(new SolidColorBrush(Color.FromArgb(alpha, tint.R, tint.G, tint.B)));
        }

        internal static Brush CreateCardBorder(double opacity)
        {
            return Frozen(new SolidColorBrush(Color.FromArgb((byte)Math.Round(40 * opacity), 255, 255, 255)));
        }

        private static Color Blend(Color first, Color second, double secondRatio)
        {
            double ratio = Math.Max(0.0, Math.Min(1.0, secondRatio));
            return Color.FromRgb(
                (byte)Math.Round(first.R * (1 - ratio) + second.R * ratio),
                (byte)Math.Round(first.G * (1 - ratio) + second.G * ratio),
                (byte)Math.Round(first.B * (1 - ratio) + second.B * ratio));
        }

        private static Color Darken(Color color, double amount)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, color.R * (1 - amount)),
                (byte)Math.Max(0, color.G * (1 - amount)),
                (byte)Math.Max(0, color.B * (1 - amount)));
        }

        private static T Frozen<T>(T value) where T : Freezable
        {
            if (value.CanFreeze) value.Freeze();
            return value;
        }
    }
}
