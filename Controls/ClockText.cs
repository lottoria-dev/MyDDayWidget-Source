using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DDay3.Controls
{
    // Center the visible glyphs, including bold/italic overhang, rather than inline whitespace.
    public sealed class ClockText : Control
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(ClockText), new FrameworkPropertyMetadata("00:00:00",
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PeriodProperty = DependencyProperty.Register(
            "Period", typeof(string), typeof(ClockText), new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PeriodFontSizeProperty = DependencyProperty.Register(
            "PeriodFontSize", typeof(double), typeof(ClockText), new FrameworkPropertyMetadata(12.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public string Text { get { return (string)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
        public string Period { get { return (string)GetValue(PeriodProperty); } set { SetValue(PeriodProperty, value); } }
        public double PeriodFontSize { get { return (double)GetValue(PeriodFontSizeProperty); } set { SetValue(PeriodFontSizeProperty, value); } }

        private sealed class Line
        {
            internal FormattedText Digits, Marker;
            internal Rect DigitsInk, MarkerInk;
            internal double Width, Height, Gap;
        }
        private Line cachedLine;
        internal Rect LastInkBounds { get; private set; }
        private double PixelsPerDip { get { return VisualTreeHelper.GetDpi(this).PixelsPerDip; } }
        private double Guard { get { return 2.0 / PixelsPerDip; } }

        public ClockText()
        {
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = false;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == TextProperty || e.Property == PeriodProperty || e.Property == PeriodFontSizeProperty ||
                e.Property == FontFamilyProperty || e.Property == FontSizeProperty || e.Property == FontWeightProperty ||
                e.Property == FontStyleProperty || e.Property == FontStretchProperty || e.Property == ForegroundProperty)
            {
                cachedLine = null;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            cachedLine = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private FormattedText Format(string text, double size)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), size, Foreground ?? Brushes.White,
                null, TextFormattingMode.Display, PixelsPerDip);
        }

        private Line CreateLine(string digits, string period, double size)
        {
            Line line = new Line { Digits = Format(digits, size) };
            line.DigitsInk = line.Digits.BuildGeometry(new Point()).Bounds;
            if (line.DigitsInk.IsEmpty) line.DigitsInk = new Rect(0, 0, 0, line.Digits.Height);
            line.Width = line.DigitsInk.Width;
            line.Height = Math.Max(line.Digits.Height, line.DigitsInk.Height);
            if (!string.IsNullOrEmpty(period))
            {
                line.Marker = Format(period, Math.Max(1, PeriodFontSize * size / FontSize));
                line.MarkerInk = line.Marker.BuildGeometry(new Point()).Bounds;
                if (line.MarkerInk.IsEmpty) line.MarkerInk = new Rect(0, 0, 0, line.Marker.Height);
                line.Gap = size * 0.16;
                line.Width += line.MarkerInk.Width + line.Gap;
                line.Height = Math.Max(line.Height, Math.Max(line.Marker.Height, line.MarkerInk.Height));
            }
            return line;
        }

        private Line CurrentLine()
        {
            return cachedLine ?? (cachedLine = CreateLine((Text ?? string.Empty).Trim(), (Period ?? string.Empty).Trim(), FontSize));
        }

        // Check all digits and both AM/PM strings; "8" is not the widest digit in every font.
        internal double MeasureReferenceWidth(string pattern, string[] periods)
        {
            double width = CurrentLine().Width;
            foreach (string period in periods)
                for (char digit = '0'; digit <= '9'; digit++)
                    width = Math.Max(width, CreateLine(pattern.Replace('#', digit), period, FontSize).Width);
            return Math.Ceiling(width + Guard * 2);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Line line = CurrentLine();
            // Returning an oversized desired width lets FrameworkElement arrange outside its slot
            // and apply a layout clip before our drawing can fit the text.
            return new Size(Math.Min(constraint.Width, Math.Ceiling(line.Width + Guard * 2)),
                Math.Ceiling(line.Height + Guard * 2));
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            LastInkBounds = Rect.Empty;
            double available = ActualWidth - Guard * 2;
            if (available <= 0 || ActualHeight <= 0) return;
            Line line = CurrentLine();
            double size = FontSize;
            // Re-format at the final size; do not scale a bitmap or clip hinted glyphs.
            for (int attempt = 0; line.Width > available && attempt < 12; attempt++)
            {
                size *= Math.Min(0.95, available / line.Width * 0.98);
                line = CreateLine((Text ?? string.Empty).Trim(), (Period ?? string.Empty).Trim(), Math.Max(0.1, size));
            }
            if (line.Width > available) return;
            double left = (ActualWidth - line.Width) / 2;
            double centerY = ActualHeight / 2;
            if (line.Marker != null)
            {
                context.DrawText(line.Marker, new Point(left - line.MarkerInk.Left,
                    centerY - line.MarkerInk.Height / 2 - line.MarkerInk.Top));
                left += line.MarkerInk.Width + line.Gap;
            }
            context.DrawText(line.Digits, new Point(left - line.DigitsInk.Left,
                centerY - line.DigitsInk.Height / 2 - line.DigitsInk.Top));
            double inkHeight = Math.Max(line.DigitsInk.Height, line.Marker == null ? 0 : line.MarkerInk.Height);
            LastInkBounds = new Rect((ActualWidth - line.Width) / 2, centerY - inkHeight / 2, line.Width, inkHeight);
        }
    }
}
