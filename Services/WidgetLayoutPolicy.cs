using System;
using DDay3.Models;

namespace DDay3.Services
{
    // A single scale for the complete widget, independent of individual font sizes.
    internal static class WidgetLayoutPolicy
    {
        internal static double MinimumReadableWidth(AppSettings settings, double designWidth)
        {
            double smallestPoints = Math.Min(settings.SizeTime, settings.SizeDate);
            if (settings.Items.Count > 0)
                smallestPoints = Math.Min(smallestPoints, Math.Min(settings.SizeDDayTitle,
                    Math.Min(settings.SizeDDayCount, settings.SizeDDayDate)));
            if (settings.ShowCalendar) smallestPoints = Math.Min(smallestPoints, settings.SizeCalendar);
            // Keep the smallest displayed glyph at least 9 DIPs.
            return Math.Max(180, Math.Ceiling(designWidth * 9.0 / (Math.Max(1, smallestPoints) * 96.0 / 72.0)));
        }

        internal static double Scale(double width, double height, double designWidth, double designHeight)
        {
            if (designWidth <= 0 || designHeight <= 0) throw new ArgumentOutOfRangeException("designWidth");
            return Math.Max(0, Math.Min(width / designWidth, height / designHeight));
        }

        internal static double ResizeWidth(double width, double dx, double dy, double aspect, double minimum)
        {
            // Project pointer movement onto the aspect-ratio diagonal. Horizontal and vertical drags work.
            double delta = (dx + dy * aspect) / (1 + aspect * aspect);
            return Math.Max(minimum, Math.Min(10000, width + delta));
        }

        internal static double Snap(double coordinate, double origin, int spacing)
        {
            if (spacing <= 0) throw new ArgumentOutOfRangeException("spacing");
            return origin + Math.Round((coordinate - origin) / spacing, MidpointRounding.AwayFromZero) * spacing;
        }
    }
}
