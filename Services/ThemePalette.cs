using System;
using System.Globalization;

namespace DDay3.Services
{
    internal sealed class ThemePalette
    {
        internal string Id { get; set; }
        internal string Seed { get; set; }
        internal string Glass { get; set; }
        // Time, date, title, count, target date, calendar.
        internal string[] Colors { get; set; }

        internal static ThemePalette Preset(string id)
        {
            switch (id)
            {
                case "lavender": return Create(id, "#C8B8F4", "#FAF7FF", "#E6DCF9", "#F8F4FF", "#C8B8F4", "#D8D0EA", "#F4EFFF");
                case "mint": return Create(id, "#B8E8D2", "#F3FFF9", "#D6F2E5", "#F4FFF9", "#84D7B3", "#C8E6D8", "#EDFFF6");
                case "sunset": return Create(id, "#F3BED7", "#FFF9F2", "#F6D9C5", "#FFF9F2", "#FF9A8B", "#E6C2B7", "#FFF0E8");
                case "yellow": return Create(id, "#F2D879", "#FFFDF2", "#F7EDC3", "#FFF9E4", "#F5D76A", "#E8D9AE", "#FFF5C9");
                case "mono": return Create(id, "#D5DEE6", "#FFFFFF", "#E4E8EC", "#FFFFFF", "#C8D0D8", "#BBC4CC", "#F1F3F5");
                default: return Create("ice", "#A8DDF0", "#FFFFFF", "#DDEFFA", "#FFFFFF", "#7ED6EE", "#C9E1EC", "#EFFBFF");
            }
        }

        private static ThemePalette Create(string id, string glass, params string[] colors)
        {
            return new ThemePalette { Id = id, Seed = glass, Glass = glass, Colors = colors };
        }

        internal static ThemePalette Generate(string seed)
        {
            Rgb rgb = Rgb.Parse(seed);
            double r = rgb.R / 255.0, g = rgb.G / 255.0, b = rgb.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double delta = max - min, light = (max + min) / 2;
            double hue = 0, saturation = 0;
            if (delta > 0.00001)
            {
                saturation = delta / (1 - Math.Abs(2 * light - 1));
                if (max == r) hue = 60 * (((g - b) / delta + 6) % 6);
                else if (max == g) hue = 60 * ((b - r) / delta + 2);
                else hue = 60 * ((r - g) / delta + 4);
            }
            // Neutral seeds stay neutral; vivid/dark seeds become related, readable pastels.
            double s = saturation < 0.08 ? saturation : Math.Max(0.28, Math.Min(0.72, saturation));
            return Create("auto", FromHsl(hue, s * 0.65, 0.80),
                FromHsl(hue, s * 0.40, 0.97),
                FromHsl(hue + 10, s * 0.55, 0.87),
                FromHsl(hue - 8, s * 0.45, 0.95),
                FromHsl(hue, s, 0.77),
                FromHsl(hue + 16, s * 0.40, 0.83),
                FromHsl(hue - 6, s * 0.55, 0.92)).WithSeed(rgb.Hex);
        }

        private ThemePalette WithSeed(string seed) { Seed = seed; return this; }

        private static string FromHsl(double hue, double saturation, double light)
        {
            hue = (hue % 360 + 360) % 360;
            double c = (1 - Math.Abs(2 * light - 1)) * saturation;
            double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
            double m = light - c / 2;
            double r = 0, g = 0, b = 0;
            if (hue < 60) { r = c; g = x; }
            else if (hue < 120) { r = x; g = c; }
            else if (hue < 180) { g = c; b = x; }
            else if (hue < 240) { g = x; b = c; }
            else if (hue < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return new Rgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255),
                (int)Math.Round((b + m) * 255)).Hex;
        }

        private struct Rgb
        {
            internal int R, G, B;
            internal Rgb(int r, int g, int b) { R = r; G = g; B = b; }
            internal string Hex { get { return "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2"); } }
            internal static Rgb Parse(string value)
            {
                int rgb;
                if (value == null || value.Length != 7 || value[0] != '#' ||
                    !int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                    throw new ArgumentException("대표 색상은 #RRGGBB 형식이어야 합니다.", "value");
                return new Rgb(rgb >> 16 & 255, rgb >> 8 & 255, rgb & 255);
            }
        }
    }
}
