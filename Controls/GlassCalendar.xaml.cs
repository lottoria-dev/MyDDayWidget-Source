using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DDay3.Services;
using DDay3.Models;

namespace DDay3.Controls
{
    public sealed class CalendarDateEventArgs : EventArgs
    {
        public DateTime Date { get; private set; }
        public CalendarDateEventArgs(DateTime date) { Date = date.Date; }
    }

    public partial class GlassCalendar : UserControl
    {
        private readonly CalendarAnnotations annotations = new CalendarAnnotations();
        private Dictionary<DateTime, string[]> holidays = new Dictionary<DateTime, string[]>();
        private Dictionary<DateTime, string[]> solarTerms = new Dictionary<DateTime, string[]>();
        private string holidayStatus = string.Empty;
        private string calendarStatus = string.Empty;
        private Dictionary<DateTime, CalendarEvent[]> calendarEvents = new Dictionary<DateTime, CalendarEvent[]>();
        internal bool OpenEventsOnSingleClick { get; set; }
        private DayOfWeek firstDayOfWeek = DayOfWeek.Sunday;
        private DateTime month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private bool english;
        private string dateFormat = "yyyy-mm-dd";
        private bool weekendColors;
        private double baseFontSize = 14;
        private double displayScale = 1;
        private Color accent = Color.FromRgb(126, 214, 238);
        public DateTime? SelectedDate { get; private set; }
        public DateTime DisplayDate { get { return month; } }
        public event EventHandler<CalendarDateEventArgs> DateActivated;
        public event EventHandler<CalendarDateEventArgs> DateContextRequested;
        public event EventHandler DisplayMonthChanged;

        public GlassCalendar()
        {
            InitializeComponent();
            RenderMonth();
        }

        internal void ApplyAppearance(string family, double fontSize, FontWeight weight,
            Brush foreground, Color accentColor, bool useEnglish, DayOfWeek firstDay, bool distinguishWeekends, string selectedDateFormat)
        {
            FontFamily = new FontFamily(family);
            baseFontSize = fontSize;
            FontWeight = weight;
            Foreground = foreground;
            accent = accentColor;
            english = useEnglish;
            dateFormat = selectedDateFormat;
            firstDayOfWeek = firstDay;
            weekendColors = distinguishWeekends;
            ApplyScale(displayScale);
            RenderMonth();
        }

        internal void ApplyScale(double scale)
        {
            displayScale = scale;
            FontSize = baseFontSize * scale;
            double cellHeight = Math.Max(28, Math.Ceiling(baseFontSize * 1.6 + 10));
            MinWidth = cellHeight * 7 * scale;
            Header.Height = Math.Max(30, baseFontSize * 1.6 + 8) * scale;
            Header.Margin = new Thickness(0, 0, 0, 5 * scale);
            Header.ColumnDefinitions[0].Width = Header.ColumnDefinitions[2].Width = new GridLength(36 * scale);
            WeekdayGrid.Height = Math.Max(22, baseFontSize * 1.6 + 4) * scale;
            WeekdayGrid.Margin = new Thickness(0, 0, 0, 3 * scale);
            DaysGrid.Height = cellHeight * 6 * scale;
            foreach (Button button in new[] { PreviousButton, MonthButton, NextButton }) ScaleDayButton(button);
            foreach (UIElement element in DaysGrid.Children)
            {
                Button button = element as Button;
                if (button != null) ScaleDayButton(button);
            }
        }

        private void ScaleDayButton(Button button)
        {
            button.FontSize = FontSize;
            button.Margin = new Thickness(displayScale);
            button.Padding = new Thickness(3 * displayScale);
            Path arrow = button.Content as Path;
            if (arrow != null)
            {
                arrow.Width = 7 * displayScale; arrow.Height = 10 * displayScale;
                arrow.Stretch = Stretch.Fill;
            }
            Grid content = button.Content as Grid;
            if (content == null) return;
            foreach (UIElement element in content.Children)
            {
                Ellipse mark = element as Ellipse;
                if (mark == null) continue;
                mark.Width = mark.Height = Math.Max(2, 4 * displayScale);
                mark.Margin = new Thickness(0, 0, 0, -3 * displayScale);
            }
        }

        internal void ApplyColors(Brush foreground, Color accentColor)
        {
            SolidColorBrush current = Foreground as SolidColorBrush;
            SolidColorBrush next = foreground as SolidColorBrush;
            if (current != null && next != null && current.Color == next.Color && accent == accentColor) return;
            Foreground = foreground;
            accent = accentColor;
            RenderMonth();
        }

        internal void SetSchedules(IEnumerable<DDayItem> items)
        {
            annotations.Replace(items);
            RenderMonth();
        }

        internal void RefreshToday() { RenderMonth(); }

        internal void SetCalendarEvents(IEnumerable<CalendarEvent> events, string status)
        {
            var entries = events.ToArray();
            calendarEvents = CalendarMonth.GetDays(month, firstDayOfWeek).Where(d => d.HasValue)
                .Select(d => d.Value).ToDictionary(d => d, d => entries.Where(e => e.OccursOn(d)).ToArray());
            calendarStatus = status ?? "";
            RenderMonth();
        }

        internal void SetPublicDates(IEnumerable<HolidayEntry> entries, IEnumerable<HolidayEntry> terms, string status)
        {
            holidays = entries.GroupBy(x => x.Date.Date).ToDictionary(x => x.Key,
                x => x.Select(v => v.Name).Distinct().ToArray());
            solarTerms = terms.GroupBy(x => x.Date.Date).ToDictionary(x => x.Key,
                x => x.Select(v => v.Name).Distinct().ToArray());
            holidayStatus = status ?? string.Empty;
            RenderMonth();
        }

        private void NotifyMonthChanged()
        {
            EventHandler handler = DisplayMonthChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RenderMonth()
        {
            if (DaysGrid == null) return;
            MonthButton.Content = english ? month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                : month.ToString("yyyy년 M월", CultureInfo.InvariantCulture);
            string monthHint = string.Join("\n", new[] { english ? "Choose year and month" : "연도·월 직접 선택", holidayStatus, calendarStatus }
                .Where(text => !string.IsNullOrWhiteSpace(text)));
            MonthButton.ToolTip = new ToolTip { Content = monthHint,
                Style = (Style)Resources["CalendarDateToolTip"], IsHitTestVisible = false };
            PreviousButton.IsEnabled = month.Year != 1 || month.Month != 1;
            NextButton.IsEnabled = month.Year != 9999 || month.Month != 12;
            string[] weekdays = english ? new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }
                : new[] { "일", "월", "화", "수", "목", "금", "토" };
            WeekdayGrid.Children.Clear();
            foreach (DayOfWeek weekday in CalendarMonth.Weekdays(firstDayOfWeek))
                WeekdayGrid.Children.Add(new TextBlock { Text = weekdays[(int)weekday], TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = DayBrush(weekday) });
            DaysGrid.Children.Clear();
            foreach (DateTime? value in CalendarMonth.GetDays(month, firstDayOfWeek))
            {
                if (!value.HasValue) { DaysGrid.Children.Add(new Border()); continue; }
                DateTime date = value.Value;
                bool current = date.Month == month.Month && date.Year == month.Year;
                Grid content = new Grid();
                content.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = DayBrush(date.DayOfWeek, holidays.ContainsKey(date)) });
                CalendarEvent[] calendarEntries;
                if (annotations.Contains(date) || solarTerms.ContainsKey(date) || (calendarEvents.TryGetValue(date, out calendarEntries) && calendarEntries.Length > 0))
                    content.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = new SolidColorBrush(accent),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, -3) });
                Button button = new Button { Style = (Style)Resources["CalendarPlainButton"], Content = content,
                    Tag = date, Opacity = current ? 1 : 0.68, ToolTip = CreateDateToolTip(date) };
                if (date == DateTime.Today)
                    button.Background = new SolidColorBrush(Color.FromArgb(42, accent.R, accent.G, accent.B));
                if (SelectedDate == date)
                {
                    button.BorderBrush = new SolidColorBrush(accent);
                    button.BorderThickness = new Thickness(1);
                }
                AutomationProperties.SetName(button, date.ToString("yyyy-MM-dd"));
                ToolTipService.SetShowDuration(button, 30000);
                button.Click += Day_OnClick;
                button.MouseDoubleClick += Day_OnDoubleClick;
                button.PreviewMouseRightButtonUp += Day_OnRightClick;
                ScaleDayButton(button);
                DaysGrid.Children.Add(button);
            }
        }

        private Brush DayBrush(DayOfWeek day, bool holiday = false)
        {
            SolidColorBrush normal = Foreground as SolidColorBrush;
            if (normal == null) return Foreground;
            string color = "#" + normal.Color.R.ToString("X2") + normal.Color.G.ToString("X2") + normal.Color.B.ToString("X2");
            return new SolidColorBrush(LiquidGlassTheme.ParseColor(TextColorModeService.CalendarDayColor(
                holiday ? DayOfWeek.Sunday : day, color, holiday || weekendColors), normal.Color));
        }

        private ToolTip CreateDateToolTip(DateTime date)
        {
            string description = annotations.Describe(date, english, dateFormat);
            string[] holidayNames, termNames;
            holidays.TryGetValue(date.Date, out holidayNames);
            solarTerms.TryGetValue(date.Date, out termNames);
            string[] names = (holidayNames ?? new string[0]).Concat(termNames ?? new string[0]).Distinct().ToArray();
            if (names.Length > 0) description += "\n" + string.Join(" · ", names);
            CalendarEvent[] calendarEntries;
            if (calendarEvents.TryGetValue(date.Date, out calendarEntries) && calendarEntries.Length > 0)
            {
                description += "\n\n" + string.Join("\n", calendarEntries.Take(6).Select(e => e.Title));
                if (calendarEntries.Length > 6) description += "\n" + (english ? "More: " : "외 ") + (calendarEntries.Length - 6);
                description += english ? "\nClick to choose events" : "\n클릭하여 일정 선택";
            }
            TextBlock text = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, MaxWidth = 300 };
            return new ToolTip
            {
                Style = (Style)Resources["CalendarDateToolTip"],
                Content = new ScrollViewer
                {
                    Content = text, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                }
            };
        }

        private void Day_OnClick(object sender, RoutedEventArgs e)
        {
            SelectedDate = (DateTime)((Button)sender).Tag;
            // Keep the clicked button alive for the second click of a double-click.
            foreach (UIElement child in DaysGrid.Children)
            {
                Button day = child as Button;
                if (day == null) continue;
                bool selected = (DateTime)day.Tag == SelectedDate.Value;
                day.BorderBrush = selected ? new SolidColorBrush(accent) : Brushes.Transparent;
                day.BorderThickness = new Thickness(selected ? 1 : 0);
            }
            e.Handled = true;
            if (OpenEventsOnSingleClick) DateActivated?.Invoke(this, new CalendarDateEventArgs(SelectedDate.Value));
        }

        private void Day_OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (OpenEventsOnSingleClick) { e.Handled = true; return; }
            SelectedDate = (DateTime)((Button)sender).Tag;
            e.Handled = true;
            EventHandler<CalendarDateEventArgs> handler = DateActivated;
            if (handler != null) handler(this, new CalendarDateEventArgs(SelectedDate.Value));
        }

        private void Day_OnRightClick(object sender, MouseButtonEventArgs e)
        {
            SelectedDate = (DateTime)((Button)sender).Tag;
            e.Handled = true;
            EventHandler<CalendarDateEventArgs> handler = DateContextRequested;
            if (handler != null) handler(this, new CalendarDateEventArgs(SelectedDate.Value));
        }

        private void Previous_OnClick(object sender, RoutedEventArgs e) { month = CalendarMonth.MoveMonth(month, -1); RenderMonth(); NotifyMonthChanged(); }
        private void Next_OnClick(object sender, RoutedEventArgs e) { month = CalendarMonth.MoveMonth(month, 1); RenderMonth(); NotifyMonthChanged(); }
        private void MonthPicker_OnClick(object sender, RoutedEventArgs e)
        {
            if (MonthPicker.IsOpen) { MonthPicker.IsOpen = false; return; }
            YearInput.Text = month.Year.ToString(CultureInfo.InvariantCulture);
            MonthPickerError.Visibility = Visibility.Collapsed;
            MonthChoices.Children.Clear();
            for (int number = 1; number <= 12; number++)
            {
                Button choice = new Button
                {
                    Content = english ? CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(number) : number + "월", Tag = number, MinHeight = 36, Margin = new Thickness(3),
                    Padding = new Thickness(4), FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(32, 48, 66)),
                    Background = number == month.Month ? new SolidColorBrush(Color.FromRgb(212, 235, 246)) : Brushes.White
                };
                choice.Click += MonthChoice_OnClick;
                MonthChoices.Children.Add(choice);
            }
            MonthPicker.IsOpen = true;
            e.Handled = true;
        }

        private void MonthPicker_OnOpened(object sender, EventArgs e)
        {
            YearInput.Focus(); YearInput.SelectAll();
        }
        private void MonthChoice_OnClick(object sender, RoutedEventArgs e)
        {
            SelectMonth((int)((Button)sender).Tag);
        }
        private void SelectMonth(int number)
        {
            DateTime selected;
            if (!CalendarMonth.TrySelect(YearInput.Text.Trim(), number, out selected))
            {
                MonthPickerError.Text = "연도는 1~9999 사이의 숫자로 입력해 주세요.";
                MonthPickerError.Visibility = Visibility.Visible;
                YearInput.Focus();
                return;
            }
            month = selected;
            MonthPicker.IsOpen = false;
            RenderMonth();
            NotifyMonthChanged();
            AppServices.Log.Info("calendar.navigate", "source=year-month-picker");
        }
        private void ChangePickerYear(int delta)
        {
            DateTime value;
            if (!CalendarMonth.TrySelect(YearInput.Text.Trim(), month.Month, out value))
            {
                SelectMonth(month.Month); // Show validation without navigating.
                return;
            }
            YearInput.Text = Math.Max(1, Math.Min(9999, value.Year + delta)).ToString(CultureInfo.InvariantCulture);
            MonthPickerError.Visibility = Visibility.Collapsed;
        }
        private void PreviousYear_OnClick(object sender, RoutedEventArgs e) { ChangePickerYear(-1); }
        private void NextYear_OnClick(object sender, RoutedEventArgs e) { ChangePickerYear(1); }
        private void CloseMonthPicker_OnClick(object sender, RoutedEventArgs e) { MonthPicker.IsOpen = false; }
        private void MonthPicker_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { MonthPicker.IsOpen = false; e.Handled = true; }
            // Enter on the year field opens the same month of that year; month buttons keep their own keyboard action.
            if (e.Key == Key.Enter && YearInput.IsKeyboardFocusWithin) { SelectMonth(month.Month); e.Handled = true; }
        }

        private void Today_OnClick(object sender, RoutedEventArgs e)
        {
            MonthPicker.IsOpen = false;
            month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            SelectedDate = DateTime.Today;
            RenderMonth();
            NotifyMonthChanged();
        }
    }
}
