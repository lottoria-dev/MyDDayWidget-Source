using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DDay3.Services;

namespace DDay3.Controls
{
    // All lighting belongs to the surface drawing behind Child. Text stays unfiltered.
    public sealed class GlassPanel : Border
    {
        internal const double BackgroundRadius = 28;
        internal const double ClockRadius = 22;
        internal const double ScheduleRadius = 14;
        public static readonly DependencyProperty ShowWhiteOutlineProperty = DependencyProperty.Register(
            nameof(ShowWhiteOutline), typeof(bool), typeof(GlassPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool ShowWhiteOutline
        {
            get { return (bool)GetValue(ShowWhiteOutlineProperty); }
            set { SetValue(ShowWhiteOutlineProperty, value); }
        }
        private static readonly Brush WhiteOutlineBrush = Frozen(new SolidColorBrush(Color.FromArgb(112, 255, 255, 255)));
        private Brush highlight, shade, rim, edgeGlaze, cavityShadow, contactShadow;
        private double lightX, lightY, amount, raisedExtension, designRadius = BackgroundRadius;
        private int relief;

        internal void SetLighting(Color color, double angle, double opacity, int depth, double referenceRadius, bool background = false)
        {
            relief = GlassLighting.NormalizeDepth(depth, background);
            double magnitude = GlassLighting.ReliefAmount(relief, background);
            amount = Math.Min(1, magnitude);
            raisedExtension = Math.Max(0, magnitude - 1);
            designRadius = Math.Max(1, referenceRadius);
            lightX = GlassLighting.X(angle); lightY = GlassLighting.Y(angle);
            double alpha = Math.Max(0, Math.Min(1, opacity));
            Color light = Color.FromRgb((byte)((color.R + 510) / 3),
                (byte)((color.G + 510) / 3), (byte)((color.B + 510) / 3));
            Color dark = Color.FromRgb(12, 18, 22);
            double reflection = GlassLighting.HighlightDirection(angle, relief);
            double hx = GlassLighting.X(reflection), hy = GlassLighting.Y(reflection);

            // Continuous fade to a flat, almost clear face at depth zero.
            highlight = Glow(light, (12 + 34 * amount) * amount * alpha, hx, hy);
            shade = Glow(dark, (10 + 42 * amount) * amount * alpha, -hx, -hy);
            if (relief == 0) rim = Frozen(new SolidColorBrush(WithAlpha(light, 32 * alpha)));
            else
            {
                var edge = Directional(hx, hy);
                edge.GradientStops.Add(new GradientStop(WithAlpha(light, (32 + 74 * amount) * alpha), 0));
                edge.GradientStops.Add(new GradientStop(WithAlpha(light, (32 - 20 * amount) * alpha), .48));
                // Both ends approach the same color and alpha as depth approaches zero.
                edge.GradientStops.Add(new GradientStop(WithAlpha(Blend(light, dark, amount),
                    (32 + 18 * amount) * alpha), 1));
                rim = Frozen(edge);
            }
            var glaze = rim.CloneCurrentValue();
            glaze.Opacity = .18 * amount;
            edgeGlaze = Frozen(glaze);
            var cavity = Directional(lightX, lightY);
            cavity.GradientStops.Add(new GradientStop(WithAlpha(dark, 96 * amount * alpha), 0));
            cavity.GradientStops.Add(new GradientStop(WithAlpha(dark, 22 * amount * alpha), .40));
            cavity.GradientStops.Add(new GradientStop(WithAlpha(dark, 0), .85));
            cavityShadow = Frozen(cavity);
            contactShadow = Frozen(new SolidColorBrush(WithAlpha(Colors.Black, 22 * amount * alpha)));
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth < 2 || ActualHeight < 2) return;
            double scale = Math.Max(.3, CornerRadius.TopLeft / designRadius);
            double radius = Math.Min(CornerRadius.TopLeft, Math.Min(ActualWidth, ActualHeight) / 2);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double inset = .5 / pixelsPerDip;
            Rect face = new Rect(inset, inset, ActualWidth - inset * 2, ActualHeight - inset * 2);
            Geometry outline = SurfaceOutline(face, radius);
            if (rim != null && relief > 0)
            {
                // Background-only extension stays inside the existing 6-DIP outer inset.
                // Cards retain their original 2-DIP shadow allowance.
                for (int i = 3; i >= 1; i--)
                {
                    double spread = i / 3.0 * (amount + raisedExtension * 2.4) * scale;
                    Rect shadow = face; shadow.Offset(-lightX * spread * .7, -lightY * spread * .7);
                    dc.DrawGeometry(null, new Pen(contactShadow, Math.Max(.1, spread * 1.6)), SurfaceOutline(shadow, radius));
                }
            }
            // Border still measures/arranges Child. Draw its face ourselves so fill, edge,
            // lighting and shadow share one continuous-corner outline without a second rim.
            dc.DrawGeometry(Background, null, outline);
            if (rim == null)
            {
                if (BorderBrush != null && BorderThickness.Left > 0)
                    dc.DrawGeometry(null, new Pen(BorderBrush, BorderThickness.Left), outline);
                DrawWhiteOutline(dc, outline, pixelsPerDip);
                return;
            }
            dc.PushClip(outline);
            if (relief != 0)
            {
                dc.DrawRectangle(shade, null, face);
                dc.DrawRectangle(highlight, null, face);
            }
            if (relief < 0)
            {
                // Negative relief has no external shadow. Shade falls inside the light-facing
                // wall, while the far shoulder catches the reflection: a recessed cavity.
                double band = (1 + 5 * amount) * scale;
                Rect cavity = face; cavity.Inflate(-band / 2, -band / 2);
                if (cavity.Width > 0 && cavity.Height > 0)
                    dc.DrawGeometry(null, new Pen(cavityShadow, band),
                        SurfaceOutline(cavity, Math.Max(0, radius - band / 2)));
            }
            if (relief != 0)
            {
                // Faint nested bands fade inward instead of forming a hard double border.
                double reach = (1.2 + amount * 1.4 + raisedExtension * 2.6) * scale;
                for (int step = 3; step >= 1; step--)
                {
                    double band = reach * step / 3;
                    Rect shoulder = face; shoulder.Inflate(-band / 2, -band / 2);
                    if (shoulder.Width > 0 && shoulder.Height > 0)
                        dc.DrawGeometry(null, new Pen(edgeGlaze, band),
                            SurfaceOutline(shoulder, Math.Max(0, radius - band / 2)));
                }
            }
            // Depth changes the shoulder and shadow, not the weight of the hairline.
            double thickness = Math.Max(.6 / pixelsPerDip, (.7 + raisedExtension * .1) * scale);
            Rect edgeRect = face; edgeRect.Inflate(-thickness / 2, -thickness / 2);
            if (edgeRect.Width > 0 && edgeRect.Height > 0)
                dc.DrawGeometry(null, new Pen(rim, thickness),
                    SurfaceOutline(edgeRect, Math.Max(0, radius - thickness / 2)));
            dc.Pop();
            DrawWhiteOutline(dc, outline, pixelsPerDip);
        }

        private void DrawWhiteOutline(DrawingContext dc, Geometry outline, double pixelsPerDip)
        {
            // One physical pixel, independent of widget scale, light tint and face opacity.
            // The outline is inset by half a pixel, so this stroke stays inside the surface.
            if (ShowWhiteOutline)
                dc.DrawGeometry(null, new Pen(WhiteOutlineBrush, 1.0 / pixelsPerDip), outline);
        }

        internal static Geometry SurfaceOutline(Rect rect, double radius)
        {
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return Geometry.Empty;
            double r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            double l = rect.Left, t = rect.Top, b = rect.Bottom, right = rect.Right;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                // Two cubics per corner approach the flat edges with zero curvature.
                // The shared diagonal point has matching tangent and curvature on both sides.
                path.BeginFigure(new Point(l + r, t), true, true);
                path.LineTo(new Point(right - r, t), true, true);
                path.BezierTo(new Point(right - .64 * r, t), new Point(right - .32 * r, t), new Point(right - .16 * r, t + .16 * r), true, true);
                path.BezierTo(new Point(right, t + .32 * r), new Point(right, t + .64 * r), new Point(right, t + r), true, true);
                path.LineTo(new Point(right, b - r), true, true);
                path.BezierTo(new Point(right, b - .64 * r), new Point(right, b - .32 * r), new Point(right - .16 * r, b - .16 * r), true, true);
                path.BezierTo(new Point(right - .32 * r, b), new Point(right - .64 * r, b), new Point(right - r, b), true, true);
                path.LineTo(new Point(l + r, b), true, true);
                path.BezierTo(new Point(l + .64 * r, b), new Point(l + .32 * r, b), new Point(l + .16 * r, b - .16 * r), true, true);
                path.BezierTo(new Point(l, b - .32 * r), new Point(l, b - .64 * r), new Point(l, b - r), true, true);
                path.LineTo(new Point(l, t + r), true, true);
                path.BezierTo(new Point(l, t + .64 * r), new Point(l, t + .32 * r), new Point(l + .16 * r, t + .16 * r), true, true);
                path.BezierTo(new Point(l + .32 * r, t), new Point(l + .64 * r, t), new Point(l + r, t), true, true);
            }
            return Frozen(geometry);
        }

        private static LinearGradientBrush Directional(double x, double y)
        { return new LinearGradientBrush { StartPoint = new Point(.5 + x * .5, .5 + y * .5), EndPoint = new Point(.5 - x * .5, .5 - y * .5) }; }

        private static Brush Glow(Color color, double opacity, double x, double y)
        {
            var brush = new RadialGradientBrush
            {
                Center = new Point(.5 + x * .40, .5 + y * .40),
                GradientOrigin = new Point(.5 + x * .40, .5 + y * .40), RadiusX = .9, RadiusY = .9
            };
            brush.GradientStops.Add(new GradientStop(WithAlpha(color, opacity), 0));
            brush.GradientStops.Add(new GradientStop(WithAlpha(color, opacity * .25), .55));
            brush.GradientStops.Add(new GradientStop(WithAlpha(color, 0), 1));
            return Frozen(brush);
        }
        private static Color Blend(Color a, Color b, double weight)
        { return Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * weight), (byte)Math.Round(a.G + (b.G - a.G) * weight), (byte)Math.Round(a.B + (b.B - a.B) * weight)); }
        private static Color WithAlpha(Color color, double opacity)
        { return Color.FromArgb((byte)Math.Max(0, Math.Min(255, Math.Round(opacity))), color.R, color.G, color.B); }
        private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
    }
}
