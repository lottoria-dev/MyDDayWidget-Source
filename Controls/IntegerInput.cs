using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DDay3.Services;

namespace DDay3.Controls
{
    // Commit whole integers, not intermediate keystrokes such as '-' or an empty field.
    public sealed class IntegerInput : TextBox
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double),
            typeof(IntegerInput), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                ValueChanged, CoerceNumber));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double),
            typeof(IntegerInput), new PropertyMetadata(0.0, RangeChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double),
            typeof(IntegerInput), new PropertyMetadata(100.0, RangeChanged));
        public static readonly DependencyProperty WrapProperty = DependencyProperty.Register("Wrap", typeof(bool),
            typeof(IntegerInput), new PropertyMetadata(false, RangeChanged));
        public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
        public double Minimum { get { return (double)GetValue(MinimumProperty); } set { SetValue(MinimumProperty, value); } }
        public double Maximum { get { return (double)GetValue(MaximumProperty); } set { SetValue(MaximumProperty, value); } }
        public bool Wrap { get { return (bool)GetValue(WrapProperty); } set { SetValue(WrapProperty, value); } }

        public IntegerInput()
        {
            Style = (Style)Application.Current?.TryFindResource(typeof(TextBox));
            TextAlignment = TextAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            Padding = new Thickness(3, 0, 3, 0);
            Margin = new Thickness(1);
            Height = 28;
            MinWidth = 40;
            MaxLength = 12;
            RefreshText();
        }

        internal void Commit()
        {
            double parsed;
            if (NumericSetting.TryParse(Text, Minimum, Maximum, Wrap, out parsed))
            {
                SetCurrentValue(ValueProperty, parsed);
                GetBindingExpression(ValueProperty)?.UpdateSource();
            }
            RefreshText();
        }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        { Commit(); base.OnLostKeyboardFocus(e); }
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Commit(); SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Escape) { RefreshText(); SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                Commit();
                SetCurrentValue(ValueProperty, NumericSetting.Normalize(Value + (e.Key == Key.Up ? 1 : -1), Minimum, Maximum, Wrap));
                GetBindingExpression(ValueProperty)?.UpdateSource();
                SelectAll(); e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }
        private void RefreshText() { Text = Value.ToString("0", CultureInfo.InvariantCulture); }
        private static object CoerceNumber(DependencyObject d, object value)
        {
            var input = (IntegerInput)d;
            return NumericSetting.Normalize((double)value, input.Minimum, input.Maximum, input.Wrap);
        }
        private static void ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { ((IntegerInput)d).RefreshText(); }
        private static void RangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { d.CoerceValue(ValueProperty); }
    }
}
