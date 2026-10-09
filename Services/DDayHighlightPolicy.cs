using System;
using System.Globalization;
using DDay3.Models;

namespace DDay3.Services
{
    internal sealed class DDayHighlightPreset
    {
        internal string Id { get; private set; }
        internal string Name { get; private set; }
        internal string Background { get; private set; }
        internal string LightTitle { get; private set; }
        internal string DarkTitle { get; private set; }

        internal DDayHighlightPreset(string id, string name, string background, string lightTitle, string darkTitle)
        { Id = id; Name = name; Background = background; LightTitle = lightTitle; DarkTitle = darkTitle; }
    }

    internal static class DDayHighlightPolicy
    {
        internal static readonly DDayHighlightPreset[] Presets =
        {
            new DDayHighlightPreset("amber", "앰버", "#C9A96B", "#F4DCA5", "#684918"),
            new DDayHighlightPreset("rose", "로즈", "#C38E99", "#F3C9D2", "#713F4B"),
            new DDayHighlightPreset("mint", "민트", "#80B4A2", "#BAE9D7", "#285D4D"),
            new DDayHighlightPreset("blue", "블루", "#86A9C9", "#C4E1F6", "#355570")
        };

        internal static DDayHighlightPreset Find(string id)
        {
            foreach (DDayHighlightPreset preset in Presets)
                if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase)) return preset;
            return Presets[0];
        }

        internal static string NormalizePreset(string id)
        { return id == "custom" ? "custom" : Find(id).Id; }

        internal static bool IsUpcoming(AppSettings value, DateTime date, DateTime today)
        {
            if (!value.HighlightUpcomingDDay) return false;
            int days = (date.Date - today.Date).Days;
            return days >= 0 && days <= 3;
        }

        internal static bool UsesDarkTitle(AppSettings value)
        {
            if (value.TextColorMode == "dark") return true;
            if (value.TextColorMode == "light") return false;
            int rgb;
            string color = value.ColorDDayTitle;
            return color != null && color.Length == 7 && color[0] == '#' &&
                int.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb) &&
                .299 * ((rgb >> 16) & 255) + .587 * ((rgb >> 8) & 255) + .114 * (rgb & 255) < 128;
        }

        internal static string Background(AppSettings value)
        { return value.UpcomingDDayPreset == "custom" ? value.UpcomingDDayBackgroundColor : Find(value.UpcomingDDayPreset).Background; }

        internal static string Title(AppSettings value)
        {
            if (value.UpcomingDDayPreset == "custom") return value.UpcomingDDayTitleColor;
            DDayHighlightPreset preset = Find(value.UpcomingDDayPreset);
            return UsesDarkTitle(value) ? preset.DarkTitle : preset.LightTitle;
        }

        internal static string NormalizeColor(string value, string fallback)
        {
            int rgb;
            return value != null && value.Length == 7 && value[0] == '#' &&
                int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)
                ? "#" + rgb.ToString("X6", CultureInfo.InvariantCulture) : fallback;
        }
    }
}
