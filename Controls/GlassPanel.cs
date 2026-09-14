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
        private Brush highlight, shade, rim, cavityShadow, contactShadow;
        private double lightX, lightY, amount, designRadius = 22;
        private int relief;

        internal void SetLighting(Color color, double angle, double opacity, int depth, double referenceRadius)
        {
            relief = GlassLighting.NormalizeDepth(depth);
            amount = GlassLighting.ReliefAmount(relief);
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
            if (relief == 0) rim = Frozen(new SolidColorBrush(WithAlpha(light, 38 * alpha)));
            else
            {
                var edge = Directional(hx, hy);
                edge.GradientStops.Add(new GradientStop(WithAlpha(light, (38 + 155 * amount) * alpha), 0));
                edge.GradientStops.Add(new GradientStop(WithAlpha(light, (38 - 25 * amount) * alpha), .48));
                // Both ends approach the same color and alpha as depth approaches zero.
                edge.GradientStops.Add(new GradientStop(WithAlpha(Blend(light, dark, amount),
                    (38 + 50 * amount) * alpha), 1));
                rim = Frozen(edge);
            }
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
            if (ActualWidth < 2 || ActualHeight < 2) { base.OnRender(dc); return; }
            double scale = Math.Max(.3, CornerRadius.TopLeft / designRadius);
            double radius = Math.Min(CornerRadius.TopLeft, Math.Min(ActualWidth, ActualHeight) / 2);
            Rect face = new Rect(.5, .5, ActualWidth - 1, ActualHeight - 1);
            if (rim != null && relief > 0)
            {
                // Positive relief casts a contact shadow outside. Its maximum reach stays
                // below the existing 2-DIP capsule margin, including the pen width.
                for (int i = 3; i >= 1; i--)
                {
                    double spread = i / 3.0 * amount * scale;
                    Rect shadow = face; shadow.Offset(-lightX * spread * .7, -lightY * spread * .7);
                    dc.DrawRoundedRectangle(null, new Pen(contactShadow, Math.Max(.1, spread * 1.6)), shadow, radius, radius);
                }
            }
            base.OnRender(dc);
            if (rim == null) return;
            dc.PushClip(new RectangleGeometry(face, radius, radius));
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
                Rect inset = face; inset.Inflate(-band / 2, -band / 2);
                if (inset.Width > 0 && inset.Height > 0)
                    dc.DrawRoundedRectangle(null, new Pen(cavityShadow, band), inset,
                        Math.Max(0, radius - band / 2), Math.Max(0, radius - band / 2));
            }
            double thickness = Math.Max(.5, (.6 + amount * 1.0) * scale);
            Rect edgeRect = face; edgeRect.Inflate(-thickness / 2, -thickness / 2);
            if (edgeRect.Width > 0 && edgeRect.Height > 0)
                dc.DrawRoundedRectangle(null, new Pen(rim, thickness), edgeRect,
                    Math.Max(0, radius - thickness / 2), Math.Max(0, radius - thickness / 2));
            dc.Pop();
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
