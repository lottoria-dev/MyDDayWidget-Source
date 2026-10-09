using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private static readonly Brush WhiteOutlineBrush = Frozen(new SolidColorBrush(Color.FromArgb(GlassReflection.OutlineAlpha, 255, 255, 255)));
        private bool lightingSet, reflectionDirty = true;
        private Color reflectionColor;
        private Color refractionColor;
        private double refractionStrength;
        private double lightAngle, layerOpacity, designRadius = BackgroundRadius;
        private int relief;
        private Geometry cachedOutline;
        private BitmapSource reflectionImage;
        private readonly System.Collections.Generic.List<EdgePixel> edgePixels = new System.Collections.Generic.List<EdgePixel>();
        private double cachedWidth, cachedHeight, cachedRadius, cachedDpi, cachedScale;
        private int pixelWidth, pixelHeight;
        private double pixelSize;
        internal int ReflectionBuildCount { get; private set; }
        internal int ContourBuildCount { get; private set; }
        private struct EdgePixel
        {
            internal int Offset;
            internal float Distance, NormalX, NormalY;
        }

        internal void SetLighting(Color color, double angle, double opacity, int depth, double referenceRadius, bool background = false,
            Color? pastelColor = null, string pastelMode = "off", double pastelWeight = 1)
        {
            int normalizedDepth = GlassLighting.NormalizeDepth(depth, background);
            double direction = GlassLighting.NormalizeDirection(angle);
            double alpha = Math.Max(0, Math.Min(1, opacity));
            double radius = Math.Max(1, referenceRadius);
            Color light = Color.FromRgb((byte)((color.R + 510) / 3),
                (byte)((color.G + 510) / 3), (byte)((color.B + 510) / 3));
            Color pastel = pastelColor ?? Colors.White;
            double strength = GlassRefraction.Strength(pastelMode, pastelWeight);
            if (lightingSet && reflectionColor == light && lightAngle == direction && layerOpacity == alpha
                && relief == normalizedDepth && designRadius == radius && refractionColor == pastel && refractionStrength == strength) return;
            lightingSet = true;
            reflectionColor = light; lightAngle = direction; layerOpacity = alpha;
            relief = normalizedDepth; designRadius = radius;
            refractionColor = pastel; refractionStrength = strength;
            reflectionDirty = true;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth < 2 || ActualHeight < 2) return;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double radius = Math.Min(CornerRadius.TopLeft, Math.Min(ActualWidth, ActualHeight) / 2);
            EnsureContour(radius, dpi);
            dc.DrawGeometry(Background, null, cachedOutline);
            if (!lightingSet)
            {
                if (BorderBrush != null && BorderThickness.Left > 0)
                    dc.DrawGeometry(null, new Pen(BorderBrush, BorderThickness.Left), cachedOutline);
            }
            else if (relief != 0 && layerOpacity > 0)
            {
                if (reflectionDirty) BuildReflection();
                // Bevel lighting stays strictly inside the face. Child text stays native WPF.
                dc.PushClip(cachedOutline);
                dc.DrawImage(reflectionImage, new Rect(0, 0, ActualWidth, ActualHeight));
                dc.Pop();
            }
            DrawWhiteOutline(dc, cachedOutline, dpi);
        }

        private void EnsureContour(double radius, double dpi)
        {
            double scale = Math.Max(.3, CornerRadius.TopLeft / designRadius);
            if (cachedOutline != null && cachedWidth == ActualWidth && cachedHeight == ActualHeight
                && cachedRadius == radius && cachedDpi == dpi && cachedScale == scale) return;
            cachedWidth = ActualWidth; cachedHeight = ActualHeight; cachedRadius = radius; cachedDpi = dpi;
            cachedScale = scale;
            double inset = .5 / dpi;
            var face = new Rect(inset, inset, ActualWidth - inset * 2, ActualHeight - inset * 2);
            cachedOutline = SurfaceOutline(face, radius);
            // Limit exceptionally large surfaces to about 8 MiB per reflection texture.
            // Ordinary sizes render at device resolution, without scaling the text or panel fill.
            double density = Math.Min(dpi, Math.Sqrt(2000000.0 / (ActualWidth * ActualHeight)));
            pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * density));
            pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * density));
            pixelSize = Math.Max(ActualWidth / pixelWidth, ActualHeight / pixelHeight);
            double reach = 10 * scale + 2 * pixelSize;
            var contour = new GlassContour(face.Left, face.Top, face.Width, face.Height, radius);
            edgePixels.Clear();
            for (int row = 0; row < pixelHeight; row++)
            {
                double y = (row + .5) * ActualHeight / pixelHeight;
                for (int column = 0; column < pixelWidth; column++)
                {
                    double x = (column + .5) * ActualWidth / pixelWidth;
                    // Central pixels are clear: only the perimeter needs a distance/normal sample.
                    if (x > radius + reach && x < ActualWidth - radius - reach
                        && y > reach && y < ActualHeight - reach) continue;
                    if (y > radius + reach && y < ActualHeight - radius - reach
                        && x > reach && x < ActualWidth - reach) continue;
                    GlassEdgePoint point = contour.Sample(x, y);
                    if (point.Distance < -pixelSize || point.Distance > reach) continue;
                    edgePixels.Add(new EdgePixel { Offset = (row * pixelWidth + column) * 4,
                        Distance = (float)point.Distance, NormalX = (float)point.NormalX, NormalY = (float)point.NormalY });
                }
            }
            reflectionDirty = true;
            ContourBuildCount++;
        }

        private void BuildReflection()
        {
            byte[] pixels = new byte[pixelWidth * pixelHeight * 4];
            double scale = Math.Max(.3, CornerRadius.TopLeft / designRadius);
            var profile = new GlassReflection(lightAngle, relief, scale, pixelSize, layerOpacity);
            double gain = GlassRefraction.Gain(refractionStrength);
            double colorWeight = GlassRefraction.ColorWeight(refractionStrength);
            foreach (EdgePixel point in edgePixels)
            {
                double light, shade;
                profile.Sample(point.Distance, point.NormalX, point.NormalY, out light, out shade);
                light *= gain;
                byte alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(light + shade)));
                int at = point.Offset;
                // The signed normal selects either reflected light or at most 8/255
                // neutral contact shade, never an external or blurred drop shadow.
                byte reflectedAlpha = light > 0 ? alpha : (byte)0;
                double white = GlassRefraction.WhiteMix(point.Distance, scale, relief);
                byte blue = RefractionChannel(reflectionColor.B, refractionColor.B, white, colorWeight);
                byte green = RefractionChannel(reflectionColor.G, refractionColor.G, white, colorWeight);
                byte red = RefractionChannel(reflectionColor.R, refractionColor.R, white, colorWeight);
                pixels[at] = (byte)((blue * reflectedAlpha + 127) / 255);
                pixels[at + 1] = (byte)((green * reflectedAlpha + 127) / 255);
                pixels[at + 2] = (byte)((red * reflectedAlpha + 127) / 255);
                pixels[at + 3] = alpha;
            }
            reflectionImage = BitmapSource.Create(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, pixelWidth * 4);
            reflectionImage.Freeze();
            reflectionDirty = false;
            ReflectionBuildCount++;
        }

        private static byte RefractionChannel(byte original, byte pastel, double white, double weight)
        {
            double tinted = pastel + (255 - pastel) * white;
            return (byte)Math.Round(original + (tinted - original) * weight);
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

        private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
    }
}
