using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DDay3.Controls
{
    public sealed class OpacityJogDial : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(double), typeof(OpacityJogDial),
            new FrameworkPropertyMetadata(90.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged, CoerceValue));

        public event EventHandler ValueChanged;

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public OpacityJogDial()
        {
            Width = 92;
            Height = 92;
            Focusable = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double size = Math.Min(ActualWidth, ActualHeight);
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double radius = Math.Max(10, size / 2.0 - 8);

            RadialGradientBrush face = new RadialGradientBrush
            {
                Center = new Point(0.38, 0.32),
                GradientOrigin = new Point(0.32, 0.26),
                RadiusX = 0.75,
                RadiusY = 0.75
            };
            face.GradientStops.Add(new GradientStop(Color.FromArgb(238, 255, 255, 255), 0));
            face.GradientStops.Add(new GradientStop(Color.FromArgb(200, 216, 239, 249), 0.72));
            face.GradientStops.Add(new GradientStop(Color.FromArgb(196, 169, 192, 216), 1));
            drawingContext.DrawEllipse(face, new Pen(new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), 1.5),
                center, radius, radius);

            double ratio = (Value - 5.0) / 95.0;
            double angle = (-140.0 + 280.0 * ratio) * Math.PI / 180.0;
            Point start = new Point(center.X + Math.Sin(angle) * radius * 0.30,
                center.Y - Math.Cos(angle) * radius * 0.30);
            Point end = new Point(center.X + Math.Sin(angle) * radius * 0.76,
                center.Y - Math.Cos(angle) * radius * 0.76);
            drawingContext.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(69, 143, 181)), 4.0)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, start, end);
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), null,
                new Point(center.X - radius * 0.23, center.Y - radius * 0.25), radius * 0.24, radius * 0.16);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            CaptureMouse();
            UpdateFromPoint(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateFromPoint(e.GetPosition(this));
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            Value += e.Delta > 0 ? 1 : -1;
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Down) { Value -= 1; e.Handled = true; }
            else if (e.Key == Key.Right || e.Key == Key.Up) { Value += 1; e.Handled = true; }
            else if (e.Key == Key.PageDown) { Value -= 5; e.Handled = true; }
            else if (e.Key == Key.PageUp) { Value += 5; e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void UpdateFromPoint(Point point)
        {
            double dx = point.X - ActualWidth / 2.0;
            double dy = point.Y - ActualHeight / 2.0;
            double degrees = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            degrees = Math.Max(-140.0, Math.Min(140.0, degrees));
            Value = Math.Round(5.0 + (degrees + 140.0) / 280.0 * 95.0);
        }

        private static object CoerceValue(DependencyObject dependencyObject, object baseValue)
        {
            return Math.Max(5.0, Math.Min(100.0, (double)baseValue));
        }

        private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            OpacityJogDial dial = (OpacityJogDial)dependencyObject;
            EventHandler handler = dial.ValueChanged;
            if (handler != null) handler(dial, EventArgs.Empty);
        }
    }
}
