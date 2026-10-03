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
        internal const int MaximumHeadAlpha = 128;
        private const double HeadSigma = .006;
        private const double TailLength = .80;
        private const double TailDecay = .65;
        private const int MaximumTailAlpha = 64;
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
        internal bool IsAnimating { get { return rendering; } }
        internal int RenderedFrameCount { get; private set; }
        internal Func<bool> HoverProbe { get; set; }
        private Window owner;
        private double cachedWidth = -1, cachedHeight, cachedRadius, cachedDpi, perimeter;
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
                double phase = clock.Elapsed.TotalSeconds / RevolutionSeconds;
                phase -= Math.Floor(phase);
                for (int i = 1; i < points.Count; i++)
                {
                    double fraction = (distances[i - 1] + distances[i]) / (2 * perimeter);
                    int alpha = ReflectionAlpha(fraction, phase, envelope);
                    if (alpha > 0) dc.DrawLine(pens[alpha], points[i - 1], points[i]);
                }
                RenderedFrameCount++;
            }
        }
        internal static int ReflectionAlpha(double fraction, double phase, double envelope)
        {
            // Positive lag is behind the clockwise-moving head. Wrap across the seam
            // so the tail follows continuously through the top-left contour origin.
            double lag = phase - fraction;
            lag -= Math.Floor(lag);
            double headDistance = Math.Min(lag, 1 - lag);
            double head = Math.Exp(-.5 * headDistance * headDistance / (HeadSigma * HeadSigma));
            double tail = lag > 0 && lag < TailLength
                ? Math.Exp(-lag / TailDecay) * (1 - lag / TailLength)
                : 0;
            // Whiten a compact section of the same one-pixel line; no larger head,
            // glow, blur, extra outline or change to the underlying glass surface.
            double alpha = head * MaximumHeadAlpha + (1 - head) * MaximumTailAlpha * tail;
            return (int)Math.Round(alpha * Math.Max(0, Math.Min(1, envelope)));
        }
        private void BuildContour(double radius, double dpi)
        {
            cachedWidth = ActualWidth; cachedHeight = ActualHeight; cachedRadius = radius; cachedDpi = dpi;
            double inset = .5 / dpi;
            var outline = GlassPanel.SurfaceOutline(new Rect(inset, inset, ActualWidth - 2 * inset, ActualHeight - 2 * inset), radius);
            var flat = outline.GetFlattenedPathGeometry(.15 / dpi, ToleranceType.Absolute);
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
            for (int alpha = 1; alpha < pens.Length; alpha++)
            {
                var brush = new SolidColorBrush(Color.FromArgb((byte)alpha, 255, 255, 255)); brush.Freeze();
                var pen = new Pen(brush, 1.0 / dpi);
                pen.Freeze(); pens[alpha] = pen;
            }
        }
        private void AddPoint(Point point)
        {
            if (points.Count > 0) perimeter += (point - points[points.Count - 1]).Length;
            points.Add(point); distances.Add(perimeter);
        }
        private void AddEdge(Point end)
        {
            Point start = points[points.Count - 1];
            int steps = Math.Max(1, (int)Math.Ceiling((end - start).Length / 3));
            for (int step = 1; step <= steps; step++) AddPoint(start + (end - start) * (step / (double)steps));
        }
    }
}
