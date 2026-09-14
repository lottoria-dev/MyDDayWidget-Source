using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DDay3.Services;

namespace DDay3.Controls
{
    public sealed class LightDirectionDial : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double),
            typeof(LightDirectionDial), new FrameworkPropertyMetadata(315.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                Changed, (d, v) => GlassLighting.NormalizeDirection((double)v)));
        public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
        public event EventHandler ValueChanged;
        public LightDirectionDial() { Width = Height = 92; Focusable = true; Cursor = Cursors.Hand; }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Point center = new Point(ActualWidth / 2, ActualHeight / 2);
            double radius = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 9);
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(231, 241, 247)),
                new Pen(IsKeyboardFocused ? Brushes.SteelBlue : Brushes.LightSlateGray, IsKeyboardFocused ? 2 : 1), center, radius, radius);
            for (int degree = 0; degree < 360; degree += 45)
            {
                double x = GlassLighting.X(degree), y = GlassLighting.Y(degree);
                dc.DrawLine(new Pen(Brushes.SlateGray, 1), new Point(center.X + x * (radius - 3), center.Y + y * (radius - 3)),
                    new Point(center.X + x * (radius - 6), center.Y + y * (radius - 6)));
            }
            double lx = GlassLighting.X(Value), ly = GlassLighting.Y(Value);
            Point lamp = new Point(center.X + lx * radius * .70, center.Y + ly * radius * .70);
            Point shadow = new Point(center.X - lx * 3, center.Y - ly * 3);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(42, 20, 45, 60)), null, shadow, 10, 10);
            dc.DrawEllipse(Brushes.White, new Pen(Brushes.LightSlateGray, 1), center, 9, 9);
            dc.DrawLine(new Pen(Brushes.SteelBlue, 2), center, lamp);
            dc.DrawEllipse(Brushes.LightGoldenrodYellow, new Pen(Brushes.SteelBlue, 1.5), lamp, 5, 5);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        { Focus(); CaptureMouse(); UpdateFromPoint(e.GetPosition(this)); e.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs e)
        { if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) { UpdateFromPoint(e.GetPosition(this)); e.Handled = true; } }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        { if (IsMouseCaptured) ReleaseMouseCapture(); e.Handled = true; }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        { if (e.Delta != 0) Value += e.Delta > 0 ? 5 : -5; e.Handled = true; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Down) { Value -= 5; e.Handled = true; }
            else if (e.Key == Key.Right || e.Key == Key.Up) { Value += 5; e.Handled = true; }
            else if (e.Key == Key.Home) { Value = 0; e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
        private void UpdateFromPoint(Point p)
        {
            double x = p.X - ActualWidth / 2, y = p.Y - ActualHeight / 2;
            if (x * x + y * y >= 16) Value = GlassLighting.FromPoint(x, y);
        }
        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { var dial = (LightDirectionDial)d; if (dial.ValueChanged != null) dial.ValueChanged(dial, EventArgs.Empty); }
    }
}
