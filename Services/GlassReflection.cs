using System;

namespace DDay3.Services
{
    // A shallow bevel attached to the actual edge, not a second closed border.
    // A tiny contact shade stays inside the face; child content is never filtered.
    internal struct GlassReflection
    {
        internal const byte OutlineAlpha = 32;
        internal const double MaximumShadeAlpha = 8;
        private readonly double sigma, center, peak, shadeSigma, shadePeak, lightX, lightY;
        internal GlassReflection(double angle, int depth, double scale, double pixelSize, double opacity, double strength = .18)
        {
            double magnitude = Math.Abs(depth) / 100.0;
            double amount = Math.Min(1, magnitude), extension = Math.Max(0, magnitude - 1);
            double alpha = Math.Max(0, Math.Min(1, opacity));
            // Surface reflections survive a clear face. Still fade continuously to zero
            // below the supported 5% minimum; do not add a bright ring at low opacity.
            double visibility = Math.Min(1, alpha / .05) * (.3 + .7 * Math.Sqrt(alpha));
            double intensity = Intensity(strength);
            double width = (.9 + 5.5 * Math.Pow(amount, .8) + 4 * extension) * scale;
            bool recessed = depth < 0;
            // A raised lip is brightest at the edge. A recess has a broader inside
            // shoulder, with at least 70% of its peak still connected to the edge.
            sigma = Math.Max(.65 * pixelSize, (recessed ? .45 : .38) * width);
            center = recessed ? .38 * width * amount : 0;
            peak = Math.Min(190, (24 + 60 * Math.Pow(amount, .8)) * Math.Sqrt(amount)
                * (1 + .25 * extension) * intensity) * visibility;
            shadeSigma = Math.Max(.65 * pixelSize, (recessed ? .44 : .32) * width);
            shadePeak = Math.Min(MaximumShadeAlpha, (4 + 2 * amount + extension) * amount) * visibility;
            double reflection = GlassLighting.HighlightDirection(angle, depth);
            lightX = GlassLighting.X(reflection); lightY = GlassLighting.Y(reflection);
        }
        internal static double Intensity(double strength)
        {
            if (double.IsNaN(strength) || double.IsInfinity(strength)) strength = .18;
            double position = (Math.Max(.1, Math.Min(1, strength)) - .1) / .9;
            return .65 + 1.25 * Math.Pow(position, .9);
        }
        internal double LightIncidence(double normalX, double normalY)
        {
            double facing = Math.Max(0, Math.Min(1, normalX * lightX + normalY * lightY));
            return facing * facing * (3 - 2 * facing);
        }
        internal double Alpha(double distance, double normalX, double normalY)
        {
            double light, shade;
            Sample(distance, normalX, normalY, out light, out shade);
            return light;
        }
        internal void Sample(double distance, double normalX, double normalY, out double light, out double shade)
        {
            double facing = Math.Max(-1, Math.Min(1, normalX * lightX + normalY * lightY));
            double incidence = Math.Abs(facing);
            incidence = incidence * incidence * (3 - 2 * incidence);
            double d = Math.Max(0, distance);
            light = shade = 0;
            // Opposing faces do not overlap or accumulate shadows. Both terms are
            // bounded, continuous at the side tangent and fully inside the panel.
            if (facing >= 0)
            {
                double across = (d - center) / sigma;
                light = peak * incidence * Math.Exp(-.5 * across * across);
            }
            else
            {
                double across = d / shadeSigma;
                shade = shadePeak * incidence * Math.Exp(-.5 * across * across);
            }
        }
    }

    internal struct GlassEdgePoint
    {
        internal double Distance, NormalX, NormalY;
        internal GlassEdgePoint(double distance, double normalX, double normalY)
        { Distance = distance; NormalX = normalX; NormalY = normalY; }
    }

    internal sealed class GlassContour
    {
        private readonly double centerX, centerY, halfWidth, halfHeight, radius;
        private readonly Segment[] quarter = new Segment[64];
        private struct Segment { internal double X, Y, DX, DY, InverseLengthSquared, NX, NY; }

        internal GlassContour(double left, double top, double width, double height, double cornerRadius)
        {
            halfWidth = width / 2; halfHeight = height / 2;
            centerX = left + halfWidth; centerY = top + halfHeight;
            radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(halfWidth, halfHeight)));
            // The same two cubic curves used by GlassPanel.SurfaceOutline, normalized.
            for (int half = 0; half < 2; half++)
                for (int step = 0; step < 32; step++)
                {
                    double x1, y1, x2, y2;
                    CornerPoint(half, step / 32.0, out x1, out y1);
                    CornerPoint(half, (step + 1) / 32.0, out x2, out y2);
                    double dx = x2 - x1, dy = y2 - y1, length2 = dx * dx + dy * dy;
                    double length = Math.Sqrt(length2);
                    quarter[half * 32 + step] = new Segment { X = x1, Y = y1, DX = dx, DY = dy,
                        InverseLengthSquared = 1 / length2, NX = -dy / length, NY = dx / length };
                }
        }

        internal GlassEdgePoint Sample(double x, double y)
        {
            double dx = x - centerX, dy = y - centerY;
            double ax = Math.Abs(dx), ay = Math.Abs(dy);
            double qx = ax - (halfWidth - radius), qy = ay - (halfHeight - radius);
            double signX = dx < 0 ? -1 : 1, signY = dy < 0 ? -1 : 1;
            if (radius <= 0 || qx <= 0 || qy <= 0)
                return halfWidth - ax < halfHeight - ay
                    ? new GlassEdgePoint(halfWidth - ax, signX, 0)
                    : new GlassEdgePoint(halfHeight - ay, 0, signY);
            qx /= radius; qy /= radius;
            double nearest = double.MaxValue, normalX = 0, normalY = 0, signed = 0;
            foreach (Segment segment in quarter)
            {
                double along = Math.Max(0, Math.Min(1,
                    ((qx - segment.X) * segment.DX + (qy - segment.Y) * segment.DY) * segment.InverseLengthSquared));
                double ex = qx - segment.X - along * segment.DX;
                double ey = qy - segment.Y - along * segment.DY;
                double distance2 = ex * ex + ey * ey;
                if (distance2 >= nearest) continue;
                nearest = distance2;
                normalX = segment.NX; normalY = segment.NY;
                signed = ex * normalX + ey * normalY;
            }
            return new GlassEdgePoint(Math.Sqrt(nearest) * radius * (signed > 0 ? -1 : 1), normalX * signX, normalY * signY);
        }

        private static void CornerPoint(int half, double t, out double x, out double y)
        {
            double s = 1 - t, b0 = s * s * s, b1 = 3 * s * s * t, b2 = 3 * s * t * t, b3 = t * t * t;
            if (half == 0) { x = .36 * b1 + .68 * b2 + .84 * b3; y = b0 + b1 + b2 + .84 * b3; }
            else { x = .84 * b0 + b1 + b2 + b3; y = .84 * b0 + .68 * b1 + .36 * b2; }
        }
    }
}
