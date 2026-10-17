using System;

namespace DDay3.Services
{
    // A narrow cut-glass edge, separate from the face tint and monochrome light.
    // The spectrum belongs to the perimeter; only its boundaries are softened.
    internal struct GlassPrism
    {
        internal readonly double Span, RimStart, RimWidth;
        private readonly double pixelSize, designScale, centerX, centerY;
        private readonly double lightX, lightY, rotation, peak, highlightGain;
        private readonly bool reversed;
        private static readonly double[,] Colors = {
            { 255, 91, 170 }, { 184, 96, 255 }, { 77, 161, 255 },
            { 66, 227, 239 }, { 115, 240, 137 }, { 250, 230, 91 },
            { 255, 164, 107 }, { 255, 91, 170 }
        };

        internal GlassPrism(int depth, double scale, double pixelSize, double shorterSide, double opacity, double strength,
            double surfaceStrength = .18, double angle = 315, double width = 0, double height = 0)
        {
            double amount = Math.Min(2, Math.Abs((double)depth) / 100);
            double level = Math.Min(1, amount);
            designScale = Math.Max(.3, Positive(scale, 1));
            this.pixelSize = Math.Max(.1, Positive(pixelSize, 1));
            shorterSide = Math.Max(0, Finite(shorterSide));
            reversed = depth < 0;
            RimWidth = Math.Min(.12 * shorterSide, Math.Max(1.15 * this.pixelSize,
                (1.1 + 2.3 * Math.Sqrt(level) + .7 * Math.Max(0, amount - 1)) * designScale));
            RimStart = reversed ? (.8 + .7 * Math.Sqrt(level)) * designScale + .15 * this.pixelSize : .10 * this.pixelSize;
            Span = Math.Min(.18 * shorterSide, RimStart + RimWidth + .7 * this.pixelSize);
            centerX = Positive(width, shorterSide * 2) / (2 * designScale);
            centerY = Positive(height, shorterSide) / (2 * designScale);
            double direction = GlassLighting.HighlightDirection(angle, depth);
            lightX = GlassLighting.X(direction); lightY = GlassLighting.Y(direction);
            rotation = (direction - 315) / 360;
            double alpha = Unit(opacity);
            double visibility = Math.Min(1, alpha / .05) * (.65 + .35 * Math.Sqrt(alpha));
            double intensity = .78 + .22 * Math.Sqrt(Unit((surfaceStrength - .1) / .9));
            highlightGain = depth == 0 ? 0 : visibility * Unit(strength) * intensity;
            // Keep the spectrum quieter on the desktop without dimming the
            // white specular highlights or changing the glass geometry.
            peak = .8 * (150 + 60 * Math.Sqrt(level)) * highlightGain;
        }

        private double Phase(double x, double y, double position)
        {
            x = Finite(x) / designScale; y = Finite(y) / designScale;
            double perimeter = (Math.Atan2(y - centerY, x - centerX) + Math.PI / 2) / (2 * Math.PI);
            double along = -x * lightY + y * lightX, across = x * lightX + y * lightY;
            double ripple = .045 * Math.Sin(along * .05 + .4 * Math.Sin(across * .027));
            return Wrap(perimeter + rotation + ripple + (reversed ? -.08 : .08) * (position - .5));
        }

        internal void Sample(double distance, double normalX, double normalY,
            out double red, out double green, out double blue, out double alpha, double x = 0, double y = 0)
        {
            double position = RimWidth > 0 ? Unit((distance - RimStart) / RimWidth) : 0;
            double phase = Phase(x, y, position);
            Spectrum(phase, out red, out green, out blue);
            alpha = 0;
            if (distance < 0 || distance >= Span || Span <= 0 || double.IsNaN(distance)) return;
            double facing = Smooth(normalX * lightX + normalY * lightY);
            // A faint spectrum continues around the unlit side. Direct light only
            // controls its brightness; it does not erase half of the glass rim.
            double illumination = .25 + .75 * facing;
            double width = RimWidth * (.91 + .07 * Math.Sin(6 * Math.PI * phase));
            double edge = Smooth((distance - RimStart + .35 * pixelSize) / (.75 * pixelSize))
                * (1 - Smooth((distance - RimStart - width + .35 * pixelSize) / (.75 * pixelSize)));
            double body = .70 + .30 * Math.Sin(Math.PI * position);
            alpha = peak * illumination * (.88 + .12 * Math.Sin(10 * Math.PI * phase)) * edge * body;
        }

        internal double WhiteHighlight(double distance, double normalX, double normalY, double x, double y)
        {
            if (distance < 0 || distance >= Span || Span <= 0 || highlightGain == 0) return 0;
            double facing = Smooth(normalX * lightX + normalY * lightY);
            double along = (-Finite(x) * lightY + Finite(y) * lightX) / designScale;
            double across = (Finite(x) * lightX + Finite(y) * lightY) / designScale;
            // Small spatial facets, rather than broad highlights caused by the
            // nonuniform angular distance along a long rectangular perimeter.
            double facet = Math.Pow(.5 + .5 * Math.Cos(along * (2 * Math.PI / 96) + .4 * Math.Sin(across * .016)), 64);
            double ridge = (distance - RimStart - .18 * RimWidth) / (.38 * pixelSize);
            return (22 + 172 * facet) * facing * facing * Math.Exp(-.5 * ridge * ridge) * highlightGain;
        }

        internal PrismPixel Pixel(double distance, double normalX, double normalY, double x = 0, double y = 0)
        {
            double red, green, blue, alpha;
            Sample(distance, normalX, normalY, out red, out green, out blue, out alpha, x, y);
            double white = WhiteHighlight(distance, normalX, normalY, x, y), remain = 1 - white / 255;
            return new PrismPixel { Red = Channel(red * alpha / 255 * remain + white),
                Green = Channel(green * alpha / 255 * remain + white),
                Blue = Channel(blue * alpha / 255 * remain + white), Alpha = Channel(alpha * remain + white) };
        }

        internal double ReflectionMask(double distance)
        { return distance < 0 || Span <= 0 ? 0 : 1 - Smooth((distance - Span + pixelSize) / pixelSize); }

        internal static PrismPixel Composite(PrismPixel color, double light, double shade,
            byte lightRed, byte lightGreen, byte lightBlue)
        {
            light = Math.Max(0, Math.Min(255, light)); shade = Math.Max(0, Math.Min(8, shade));
            double remain = 1 - color.Alpha / 255.0;
            return new PrismPixel {
                Red = Channel(color.Red + lightRed * light * remain / 255),
                Green = Channel(color.Green + lightGreen * light * remain / 255),
                Blue = Channel(color.Blue + lightBlue * light * remain / 255),
                Alpha = Channel(color.Alpha + (light + shade * (1 - light / 255)) * remain)
            };
        }

        internal static void Spectrum(double position, out double red, out double green, out double blue)
        {
            int segments = Colors.GetLength(0) - 1;
            double part = Wrap(position) * segments;
            int index = Math.Min(segments - 1, (int)part);
            double blend = Smooth(part - index);
            red = Colors[index, 0] + (Colors[index + 1, 0] - Colors[index, 0]) * blend;
            green = Colors[index, 1] + (Colors[index + 1, 1] - Colors[index, 1]) * blend;
            blue = Colors[index, 2] + (Colors[index + 1, 2] - Colors[index, 2]) * blend;
        }
        private static byte Channel(double value) { return (byte)Math.Round(Math.Max(0, Math.Min(255, value))); }
        private static double Smooth(double value) { value = Unit(value); return value * value * (3 - 2 * value); }
        private static double Wrap(double value) { value = Finite(value); return value - Math.Floor(value); }
        private static double Unit(double value) { return Math.Max(0, Math.Min(1, Finite(value))); }
        private static double Finite(double value) { return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value; }
        private static double Positive(double value, double fallback) { return Finite(value) > 0 ? value : fallback; }
    }
    internal struct PrismPixel { internal byte Red, Green, Blue, Alpha; }
}
