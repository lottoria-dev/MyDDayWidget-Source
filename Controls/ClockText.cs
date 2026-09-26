using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DDay3.Controls
{
    // Fixed digit cells keep the clock still; measure ink as well as advances to protect overhang.
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
            internal Glyph[] Digits;
            internal Point[] Origins;
            internal FormattedText Marker;
            internal Rect DigitsInk, MarkerInk;
            internal double Width, Height, Gap, Top, Bottom;
        }
        private sealed class Glyph
        {
            internal FormattedText Text;
            internal Rect Ink;
            internal double Left, Width;
        }
        private sealed class GlyphSet
        {
            internal readonly Dictionary<char, Glyph> Values = new Dictionary<char, Glyph>();
            internal double Size, Left, Right, Top, Bottom, Height;
        }
        private Line cachedLine;
        private GlyphSet cachedGlyphs;
        internal Rect LastInkBounds { get; private set; }
        internal Rect LastLayoutBounds { get; private set; }
        internal double[] LastDigitOrigins { get; private set; }
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
                if (e.Property != TextProperty && e.Property != PeriodProperty) cachedGlyphs = null;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            cachedLine = null;
            cachedGlyphs = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private FormattedText Format(string text, double size)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), size, Foreground ?? Brushes.White,
                null, TextFormattingMode.Display, PixelsPerDip);
        }

        private Glyph ReadGlyph(char value, double size)
        {
            var text = Format(value.ToString(), size);
            Rect ink = text.BuildGeometry(new Point()).Bounds;
            if (ink.IsEmpty) ink = new Rect(0, 0, 0, text.Height);
            double left = Math.Min(0, ink.Left);
            return new Glyph { Text = text, Ink = ink, Left = left,
                Width = Math.Max(text.WidthIncludingTrailingWhitespace, ink.Right) - left };
        }

        private GlyphSet Glyphs(double size)
        {
            if (cachedGlyphs != null && cachedGlyphs.Size == size) return cachedGlyphs;
            var set = new GlyphSet { Size = size, Top = double.PositiveInfinity };
            for (char digit = '0'; digit <= '9'; digit++)
            {
                Glyph glyph = ReadGlyph(digit, size);
                set.Values.Add(digit, glyph);
                set.Left = Math.Min(set.Left, glyph.Left);
                set.Right = Math.Max(set.Right, glyph.Left + glyph.Width);
                set.Top = Math.Min(set.Top, glyph.Ink.Top);
                set.Bottom = Math.Max(set.Bottom, glyph.Ink.Bottom);
                set.Height = Math.Max(set.Height, glyph.Text.Height);
            }
            cachedGlyphs = set;
            return set;
        }

        private Line CreateLine(string digits, string period, double size)
        {
            GlyphSet set = Glyphs(size);
            var line = new Line { Digits = new Glyph[digits.Length], Origins = new Point[digits.Length],
                DigitsInk = Rect.Empty, Top = set.Top, Bottom = set.Bottom, Height = set.Height };
            double digitWidth = Math.Ceiling((set.Right - set.Left) * PixelsPerDip) / PixelsPerDip;
            for (int i = 0; i < digits.Length; i++)
            {
                char value = digits[i];
                Glyph glyph;
                if (!set.Values.TryGetValue(value, out glyph)) set.Values[value] = glyph = ReadGlyph(value, size);
                bool number = value >= '0' && value <= '9';
                line.Digits[i] = glyph;
                line.Origins[i] = new Point(line.Width - (number ? set.Left : glyph.Left), 0);
                Rect ink = glyph.Ink; ink.Offset(line.Origins[i].X, 0); line.DigitsInk.Union(ink);
                line.Top = Math.Min(line.Top, glyph.Ink.Top);
                line.Bottom = Math.Max(line.Bottom, glyph.Ink.Bottom);
                line.Width += number ? digitWidth : glyph.Width;
            }
            line.Height = Math.Max(line.Height, line.Bottom - line.Top);
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

        // Glyphs() already measures all ten digits. Only the marker and pattern length vary.
        internal double MeasureReferenceWidth(string pattern, string[] periods)
        {
            double width = CurrentLine().Width;
            foreach (string period in periods)
                width = Math.Max(width, CreateLine(pattern.Replace('#', '0'), period, FontSize).Width);
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
            LastLayoutBounds = Rect.Empty;
            LastDigitOrigins = new double[0];
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
            LastLayoutBounds = new Rect(left, 0, line.Width, ActualHeight);
            if (line.Marker != null)
            {
                context.DrawText(line.Marker, new Point(left - line.MarkerInk.Left,
                    centerY - line.MarkerInk.Height / 2 - line.MarkerInk.Top));
                LastInkBounds = new Rect(left, centerY - line.MarkerInk.Height / 2,
                    line.MarkerInk.Width, line.MarkerInk.Height);
                left += line.MarkerInk.Width + line.Gap;
            }
            double top = centerY - (line.Bottom + line.Top) / 2;
            LastDigitOrigins = new double[line.Digits.Length];
            for (int i = 0; i < line.Digits.Length; i++)
            {
                Point origin = line.Origins[i]; origin.Offset(left, top);
                LastDigitOrigins[i] = origin.X;
                context.DrawText(line.Digits[i].Text, origin);
            }
            if (!line.DigitsInk.IsEmpty)
            {
                Rect ink = line.DigitsInk; ink.Offset(left, top);
                Rect combined = LastInkBounds; combined.Union(ink); LastInkBounds = combined;
            }
        }
    }
}
