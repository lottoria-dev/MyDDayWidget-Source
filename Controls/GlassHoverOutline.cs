using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace DDay3.Controls
{
    // A separate drawing layer: animating the rim never rebuilds the glass texture or text.
    public sealed class GlassHoverOutline : FrameworkElement
    {
        internal const double RevolutionSeconds = 16.0;
        internal const int MaximumHeadAlpha = 255;
        internal const double HeadSigma = .007;
        internal const double TailStrokePixels = 1.0;
        internal const double HeadStrokePixels = 1.4;
        internal const double HeadHaloPixels = 3.0;
        internal const int MaximumActiveRimAlpha = 104;
        private const int MaximumActiveHaloAlpha = 22;
        private const int MaximumActiveContrastAlpha = 26;
        private const double TailLength = .94;
        private const double TailDecay = 2.4;
        private const int MaximumTailAlpha = 156;
        private const int MaximumHeadStrokeAlpha = 238;
        private const int MaximumHeadHaloAlpha = 52;
        private const int MaximumContrastHeadAlpha = 102;
        private const int MaximumContrastTailAlpha = 43;
        private const double FrameInterval = 1.0 / 30;
        public static readonly DependencyProperty IsHoveringProperty = DependencyProperty.Register(
            nameof(IsHovering), typeof(bool), typeof(GlassHoverOutline), new PropertyMetadata(false, StateChanged));
        public static readonly DependencyProperty IsEffectEnabledProperty = DependencyProperty.Register(
            nameof(IsEffectEnabled), typeof(bool), typeof(GlassHoverOutline), new PropertyMetadata(true, StateChanged));
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(GlassHoverOutline),
            new FrameworkPropertyMetadata(new CornerRadius(28), FrameworkPropertyMetadataOptions.AffectsRender));
        public bool IsHovering { get { return (bool)GetValue(IsHoveringProperty); } set { SetValue(IsHoveringProperty, value); } }
        public bool IsEffectEnabled { get { return (bool)GetValue(IsEffectEnabledProperty); } set { SetValue(IsEffectEnabledProperty, value); } }
        public CornerRadius CornerRadius { get { return (CornerRadius)GetValue(CornerRadiusProperty); } set { SetValue(CornerRadiusProperty, value); } }
        private readonly DrawingVisual reflection = new DrawingVisual();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly List<Point> points = new List<Point>();
        private readonly List<double> distances = new List<double>();
        private readonly Pen[] pens = new Pen[MaximumHeadAlpha + 1];
        private readonly Pen[] headPens = new Pen[MaximumHeadStrokeAlpha + 1];
        private readonly Pen[] haloPens = new Pen[MaximumHeadHaloAlpha + 1];
        private readonly Pen[] contrastHeadPens = new Pen[MaximumContrastHeadAlpha + 1];
        private readonly Pen[] contrastTailPens = new Pen[MaximumContrastTailAlpha + 1];
        private readonly Pen[] activeRimPens = new Pen[MaximumActiveRimAlpha + 1];
        private readonly Pen[] activeHaloPens = new Pen[MaximumActiveHaloAlpha + 1];
        private readonly Pen[] activeContrastPens = new Pen[MaximumActiveContrastAlpha + 1];
        private Geometry cachedOutline;
        private StrokeAlphas[] frameAlphas;
        private struct StrokeAlphas
        {
            internal int Center, Core, Halo, ContrastHead, ContrastTail;
        }
        internal bool IsAnimating { get { return rendering; } }
        internal int RenderedFrameCount { get; private set; }
        internal Func<bool> HoverProbe { get; set; }
        private Window owner;
        private double cachedWidth = -1, cachedHeight, cachedRadius, cachedDpi, perimeter;
        private double cachedPenDpi = -1;
        private double envelope, transitionFrom, transitionAt, nextFrameAt;
        private bool target, rendering;

        public GlassHoverOutline()
        {
            IsHitTestVisible = false; Focusable = false;
            AddVisualChild(reflection);
            Loaded += OnLoaded; Unloaded += OnUnloaded; IsVisibleChanged += VisibilityChanged;
        }
        protected override int VisualChildrenCount { get { return 1; } }
        protected override Visual GetVisualChild(int index)
        { if (index != 0) throw new ArgumentOutOfRangeException(nameof(index)); return reflection; }
        private static void StateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { ((GlassHoverOutline)d).UpdateState(); }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            owner = Window.GetWindow(this);
            if (owner != null) owner.StateChanged += OwnerStateChanged;
            UpdateState();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (owner != null) owner.StateChanged -= OwnerStateChanged;
            owner = null;
            Stop();
        }
        private void OwnerStateChanged(object sender, EventArgs e) { UpdateState(); }
        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { UpdateState(); }
        private void UpdateState()
        {
            if (!IsLoaded || !IsVisible || !IsEffectEnabled
                || (owner != null && owner.WindowState == WindowState.Minimized)) { Stop(); return; }
            bool next = IsHovering;
            if (next == target && rendering) return;
            if (!next && !rendering) return;
            if (!rendering) { clock.Restart(); envelope = 0; nextFrameAt = 0; }
            transitionFrom = envelope; transitionAt = clock.Elapsed.TotalSeconds; target = next;
            if (!rendering) { rendering = true; CompositionTarget.Rendering += RenderFrame; }
        }
        private void Stop()
        {
            if (rendering) { CompositionTarget.Rendering -= RenderFrame; rendering = false; }
            clock.Reset(); target = false; envelope = 0;
            DrawFrame();
        }
        internal void RefreshPresentation()
        {
            if (HoverProbe != null) IsHovering = HoverProbe();
            UpdateState();
            if (rendering) { nextFrameAt = 0; AdvanceFrame(); }
        }
        private void RenderFrame(object sender, EventArgs e) { AdvanceFrame(); }
        private void AdvanceFrame()
        {
            // Tooltips live in another presentation source. Their mouse leave must
            // not interrupt the comet while the screen pointer is still on the widget.
            if (!rendering || clock.Elapsed.TotalSeconds < nextFrameAt) return;
            if (HoverProbe != null) IsHovering = HoverProbe();
            if (!rendering) return;
            double elapsed = clock.Elapsed.TotalSeconds - transitionAt;
            double amount = Math.Min(1, elapsed / (target ? .24 : .32));
            amount = amount * amount * (3 - 2 * amount);
            envelope = transitionFrom + ((target ? 1 : 0) - transitionFrom) * amount;
            if (!target && elapsed >= .32) { Stop(); return; }
            nextFrameAt = clock.Elapsed.TotalSeconds + FrameInterval;
            DrawFrame();
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            DrawFrame();
        }
        private void DrawFrame()
        {
            // Commit the child visual directly. InvalidateVisual would invalidate
            // arrange as well, leaving each animation frame dependent on layout.
            using (DrawingContext dc = reflection.RenderOpen())
            {
                if (envelope <= 0 || ActualWidth <= 2 || ActualHeight <= 2) return;
                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                double radius = CornerRadius.TopLeft;
                if (cachedWidth != ActualWidth || cachedHeight != ActualHeight || cachedRadius != radius || cachedDpi != dpi)
                    BuildContour(radius, dpi);
                if (perimeter <= 0) return;
                // A steady fine rim signals activation even where the moving tail fades.
                // Keep the faint halo inside the shared contour, without blur or shadow.
                dc.PushClip(cachedOutline);
                int rim = ActiveRimAlpha(envelope);
                int halo = ScaledAlpha(MaximumActiveHaloAlpha, envelope);
                int contrast = ScaledAlpha(MaximumActiveContrastAlpha, envelope);
                if (contrast > 0) dc.DrawGeometry(null, activeContrastPens[contrast], cachedOutline);
                if (halo > 0) dc.DrawGeometry(null, activeHaloPens[halo], cachedOutline);
                dc.Pop();
                // This one-pixel stroke is already half-pixel inset into the widget.
                if (rim > 0) dc.DrawGeometry(null, activeRimPens[rim], cachedOutline);
                dc.PushClip(cachedOutline);
                double phase = clock.Elapsed.TotalSeconds / RevolutionSeconds;
                phase -= Math.Floor(phase);
                for (int i = 1; i < points.Count; i++)
                {
                    double fraction = (distances[i - 1] + distances[i]) / (2 * perimeter);
                    double head = HeadWeight(fraction, phase), tail = TailWeight(fraction, phase);
                    StrokeAlphas sample = new StrokeAlphas {
                        Center = ScaledAlpha(head * MaximumHeadAlpha + (1 - head) * MaximumTailAlpha * tail, envelope),
                        Core = ScaledAlpha(head * MaximumHeadStrokeAlpha, envelope),
                        Halo = ScaledAlpha(head * MaximumHeadHaloAlpha, envelope),
                        ContrastHead = ScaledAlpha(head * MaximumContrastHeadAlpha, envelope),
                        ContrastTail = ScaledAlpha((1 - head) * MaximumContrastTailAlpha * tail, envelope)
                    };
                    frameAlphas[i - 1] = sample;
                    // A thin moving neutral rim separates white light from pale faces
                    // and wallpaper. It is not a static border or a blurred shadow.
                    if (sample.ContrastTail > 0) dc.DrawLine(contrastTailPens[sample.ContrastTail], points[i - 1], points[i]);
                    if (sample.ContrastHead > 0) dc.DrawLine(contrastHeadPens[sample.ContrastHead], points[i - 1], points[i]);
                }
                // Complete the contrast pass first: round segment ends must never
                // paint gray over the bright white core of a preceding segment.
                for (int i = 1; i < points.Count; i++)
                {
                    StrokeAlphas sample = frameAlphas[i - 1];
                    if (sample.Halo > 0) dc.DrawLine(haloPens[sample.Halo], points[i - 1], points[i]);
                    if (sample.Core > 0) dc.DrawLine(headPens[sample.Core], points[i - 1], points[i]);
                    if (sample.Center > 0) dc.DrawLine(pens[sample.Center], points[i - 1], points[i]);
                }
                dc.Pop();
                RenderedFrameCount++;
            }
        }
        internal static int ReflectionAlpha(double fraction, double phase, double envelope)
        {
            // Positive lag is behind the clockwise-moving head. Wrap across the seam
            // so the tail follows continuously through the top-left contour origin.
            double head = HeadWeight(fraction, phase);
            double tail = TailWeight(fraction, phase);
            // Keep the compact white head, with a brighter tail across most of the rim.
            double alpha = head * MaximumHeadAlpha + (1 - head) * MaximumTailAlpha * tail;
            return ScaledAlpha(alpha, envelope);
        }
        private static double TailWeight(double fraction, double phase)
        {
            double lag = phase - fraction; lag -= Math.Floor(lag);
            return lag > 0 && lag < TailLength ? Math.Exp(-lag / TailDecay) * Math.Pow(1 - lag / TailLength, .7) : 0;
        }
        internal static int ActiveRimAlpha(double envelope)
        { return ScaledAlpha(MaximumActiveRimAlpha, envelope); }
        private static int ScaledAlpha(double alpha, double envelope)
        { return (int)Math.Round(alpha * Math.Max(0, Math.Min(1, envelope))); }
        private static double HeadWeight(double fraction, double phase)
        {
            double lag = phase - fraction; lag -= Math.Floor(lag);
            double distance = Math.Min(lag, 1 - lag);
            return Math.Exp(-.5 * distance * distance / (HeadSigma * HeadSigma));
        }
        internal static int HeadStrokeAlpha(double fraction, double phase, double envelope)
        { return ScaledAlpha(MaximumHeadStrokeAlpha * HeadWeight(fraction, phase), envelope); }
        internal static int HeadHaloAlpha(double fraction, double phase, double envelope)
        { return ScaledAlpha(MaximumHeadHaloAlpha * HeadWeight(fraction, phase), envelope); }
        internal static int ContrastHeadAlpha(double fraction, double phase, double envelope)
        { return ScaledAlpha(MaximumContrastHeadAlpha * HeadWeight(fraction, phase), envelope); }
        internal static int ContrastTailAlpha(double fraction, double phase, double envelope)
        { return ScaledAlpha(MaximumContrastTailAlpha * (1 - HeadWeight(fraction, phase)) * TailWeight(fraction, phase), envelope); }
        private void BuildContour(double radius, double dpi)
        {
            cachedWidth = ActualWidth; cachedHeight = ActualHeight; cachedRadius = radius; cachedDpi = dpi;
            double inset = .5 / dpi;
            cachedOutline = GlassPanel.SurfaceOutline(new Rect(inset, inset, ActualWidth - 2 * inset, ActualHeight - 2 * inset), radius);
            var flat = cachedOutline.GetFlattenedPathGeometry(.15 / dpi, ToleranceType.Absolute);
            points.Clear(); distances.Clear(); perimeter = 0;
            foreach (var figure in flat.Figures)
            {
                AddPoint(figure.StartPoint);
                foreach (var segment in figure.Segments)
                {
                    var line = segment as LineSegment;
                    var poly = segment as PolyLineSegment;
                    if (line != null) AddEdge(line.Point);
                    if (poly != null) foreach (Point point in poly.Points) AddEdge(point);
                }
                if (figure.IsClosed) AddEdge(figure.StartPoint);
            }
            frameAlphas = new StrokeAlphas[Math.Max(0, points.Count - 1)];
            EnsurePens(dpi);
        }
        private void EnsurePens(double dpi)
        {
            if (cachedPenDpi == dpi) return;
            cachedPenDpi = dpi;
            for (int alpha = 1; alpha < pens.Length; alpha++)
                pens[alpha] = CometPen(alpha, TailStrokePixels / dpi);
            for (int alpha = 1; alpha < headPens.Length; alpha++)
                headPens[alpha] = CometPen(alpha, HeadStrokePixels / dpi);
            for (int alpha = 1; alpha < haloPens.Length; alpha++)
                haloPens[alpha] = CometPen(alpha, HeadHaloPixels / dpi);
            for (int alpha = 1; alpha < contrastHeadPens.Length; alpha++)
                contrastHeadPens[alpha] = CometPen(alpha, 2.2 / dpi, true);
            for (int alpha = 1; alpha < contrastTailPens.Length; alpha++)
                contrastTailPens[alpha] = CometPen(alpha, 1.8 / dpi, true);
            for (int alpha = 1; alpha < activeRimPens.Length; alpha++)
                activeRimPens[alpha] = CometPen(alpha, 1.0 / dpi);
            for (int alpha = 1; alpha < activeHaloPens.Length; alpha++)
                activeHaloPens[alpha] = CometPen(alpha, 3.0 / dpi);
            for (int alpha = 1; alpha < activeContrastPens.Length; alpha++)
                activeContrastPens[alpha] = CometPen(alpha, 2.0 / dpi, true);
        }
        private static Pen CometPen(int alpha, double width, bool contrast = false)
        {
            Color color = contrast ? Color.FromArgb((byte)alpha, 40, 44, 50) : Color.FromArgb((byte)alpha, 255, 255, 255);
            var brush = new SolidColorBrush(color); brush.Freeze();
            var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze(); return pen;
        }
        private void AddPoint(Point point)
        {
            if (points.Count > 0) perimeter += (point - points[points.Count - 1]).Length;
            points.Add(point); distances.Add(perimeter);
        }
        private void AddEdge(Point end)
        {
            Point start = points[points.Count - 1];
            int steps = Math.Max(1, (int)Math.Ceiling((end - start).Length * cachedDpi / 2));
            for (int step = 1; step <= steps; step++) AddPoint(start + (end - start) * (step / (double)steps));
        }
    }
}
