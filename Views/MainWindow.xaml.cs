using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DDay3.Controls;
using DDay3.Models;
using DDay3.Services;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;
using DrawingRectangle = System.Drawing.Rectangle;

namespace DDay3.Views
{
    public partial class MainWindow : Window
    {
        private readonly ConfigurationService configuration;
        private readonly DispatcherTimer clockTimer;
        private AppSettings settings;
        private AppSettings displaySettings;
        private AppSettings previewOriginal;
        private double previewOriginalWidth;
        internal double DisplayScale { get { return currentScale; } }
        internal int CalendarYear { get { return CalendarView.DisplayDate.Year; } }
        internal void RefreshHolidayDisplay() { if (IsLoaded && !quitting) RefreshCalendarHolidays(); }
        private ConfigLoadResult initialLoadResult;
        private Forms.NotifyIcon trayIcon;
        private bool quitting;
        private bool measuringLayout;
        private readonly ScheduleCarousel carousel = new ScheduleCarousel();
        private readonly StopwatchSession stopwatch = new StopwatchSession();
        private double designHeight = 250;
        private double designWidth = 350;
        private double rowHeight;
        private double currentScale = 1;
        private DateTime lastCountDate = DateTime.MinValue;
        private int holidayRequestVersion;
        private CancellationTokenSource calendarRequest;
        private List<CalendarEvent> calendarMonthEvents = new List<CalendarEvent>();
        private bool calendarDialogOpen;

        internal MainWindow(AppSettings settings, ConfigLoadResult loadResult)
        {
            InitializeComponent();
            HoverOutline.HoverProbe = IsPointerInsideWidget;
            Root.AddHandler(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(Root_OnToolTipTransition), true);
            Root.AddHandler(ToolTipService.ToolTipClosingEvent, new ToolTipEventHandler(Root_OnToolTipTransition), true);
            CalendarView.DisplayMonthChanged += delegate { RefreshCalendarHolidays(); RefreshIcsCalendar(true); };
            AppServices.IcsCalendars.Changed += IcsFiles_OnChanged;
            this.settings = settings;
            configuration = AppServices.Configuration;
            initialLoadResult = loadResult;

            Left = settings.X;
            Top = settings.Y;
            Width = settings.Width;
            Height = settings.Height;
            ApplySettings();
            BuildTrayIcon();
            BuildContextMenu();

            clockTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            clockTimer.Tick += delegate { UpdateClockAndCounts(false); UpdateTimerInterval(); };
            UpdateTimerInterval();
            clockTimer.Start();

            Loaded += OnLoaded;
            Closing += OnClosing;
            MouseLeftButtonDown += OnWindowMouseLeftButtonDown;
            SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                if (!measuringLayout) ApplyWindowSize(Width);
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HoverOutline.IsHovering = IsPointerInsideWidget();
            UpdateClockAndCounts(true);
            UpdateDesignSize();
            RefreshCalendarHolidays();
            RefreshIcsCalendar(true);
            AppServices.Log.Info("window.loaded", GeometrySummary() +
                ", monitors=" + Forms.Screen.AllScreens.Length);
            ShowConfigurationNotice();
        }

        private void Root_OnMouseEnter(object sender, MouseEventArgs e)
        { HoverOutline.IsHovering = true; }
        private void Root_OnMouseLeave(object sender, MouseEventArgs e)
        { HoverOutline.IsHovering = IsPointerInsideWidget(); }

        private void Root_OnToolTipTransition(object sender, ToolTipEventArgs e)
        {
            bool opening = e.RoutedEvent == ToolTipService.ToolTipOpeningEvent;
            // Let the tooltip finish creating/removing its popup source, then commit
            // a fresh outline frame without restarting its phase or fading it out.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (quitting || !IsLoaded) return;
                HoverOutline.RefreshPresentation();
                if (AppServices.Log != null && AppServices.Log.IsEnabled)
                    AppServices.Log.Info("hover.tooltip", (opening ? "opening" : "closing")
                        + ", rendering=" + HoverOutline.IsAnimating + ", pointer=" + HoverOutline.IsHovering
                        + ", frames=" + HoverOutline.RenderedFrameCount);
            }));
        }

        private bool IsPointerInsideWidget()
        {
            if (quitting || !IsVisible || !Root.IsLoaded || WindowState == WindowState.Minimized) return false;
            var cursor = Forms.Cursor.Position;
            Point local = Root.PointFromScreen(new Point(cursor.X, cursor.Y));
            return new Rect(Root.RenderSize).Contains(local);
        }

        private void ApplySettings(bool preserveScale = false, bool resetCarousel = true)
        {
            double previousScale = currentScale;
            LiquidGlassTheme.ApplyApplicationAccent(settings);
            Topmost = settings.Topmost;
            ApplySurfaceSettings(settings);

            TimeText.FontFamily = new FontFamily(settings.FontTime);
            TimeText.FontWeight = ParseWeight(settings.WeightTime);
            TimeText.FontSize = settings.SizeTime * 96.0 / 72.0;
            TimeText.PeriodFontSize = Math.Max(9, TimeText.FontSize * 0.34);
            SetTextStyle(DateText, settings.FontDate, settings.SizeDate, displaySettings.ColorDate, ParseWeight(settings.WeightDate));
            SetTextStyle(LunarDateText, settings.FontDate, settings.SizeDate, displaySettings.ColorDate, ParseWeight(settings.WeightDate));
            LunarDateText.Visibility = settings.ShowLunarDate ? Visibility.Visible : Visibility.Collapsed;
            StopwatchControls.Visibility = settings.ClockMode == "stopwatch" ? Visibility.Visible : Visibility.Collapsed;
            if (settings.ClockMode != "stopwatch") stopwatch.Pause();
            UpdateTimerInterval();
            Brush calendarBrush = BrushFrom(displaySettings.ColorCalendar, Colors.White);
            CalendarView.ApplyAppearance(settings.FontCalendar, settings.SizeCalendar * 96.0 / 72.0,
                ParseWeight(settings.WeightCalendar), calendarBrush,
                LiquidGlassTheme.ParseColor(displaySettings.ColorDDayCount, Colors.LightBlue), settings.DayFormat == "eng", settings.WeekStart == "Monday" ? DayOfWeek.Monday : DayOfWeek.Sunday, settings.CalendarWeekendColors, settings.DateFormat);
            CalendarHost.Visibility = settings.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;

            if (resetCarousel) carousel.Reset();
            rowHeight = MeasureScheduleRowHeight();
            RebuildDDayItems();
            UpdateCalendarMarks();
            UpdateClockAndCounts(true);
            UpdateDesignSize(preserveScale ? (double?)previousScale : null);
            if (IsLoaded) { RefreshCalendarHolidays(); RefreshIcsCalendar(true); }
        }

        // Brush alpha changes the surfaces only. Never fade the parent Window.
        internal AppSettings CreateAppearancePreview() { return (previewOriginal ?? settings).Clone(); }

        internal void PreviewAppearance(AppSettings preview)
        {
            if (previewOriginal == null)
            {
                previewOriginal = settings;
                previewOriginalWidth = Width;
            }
            settings = preview;
            ApplySettings(true, false);
        }

        internal void PreviewGlassRelief(double direction, int background, int clock, int dday, bool showPanelOutline, bool showHoverReflection)
        {
            if (previewOriginal == null)
            {
                previewOriginal = settings;
                previewOriginalWidth = Width;
                settings = settings.Clone();
            }
            settings.GlassLightDirection = GlassLighting.NormalizeDirection(direction);
            settings.PanelDepth = GlassLighting.NormalizeDepth(background, true);
            settings.ClockPanelDepth = GlassLighting.NormalizeDepth(clock);
            settings.DDayPanelDepth = GlassLighting.NormalizeDepth(dday);
            settings.ShowPanelOutline = showPanelOutline;
            settings.ShowHoverReflection = showHoverReflection;
            HoverOutline.IsEffectEnabled = showHoverReflection;
            // Pointer motion changes surface drawing only: no typography layout, calendar
            // rebuilding, clock restart, or public-data requests while turning the dial.
            ApplyPanelLighting(GlassSurface, settings, settings.PanelOpacity, settings.PanelDepth, GlassPanel.BackgroundRadius, true);
            ApplyPanelLighting(ClockCard, settings, settings.ClockPanelOpacity, settings.ClockPanelDepth, GlassPanel.ClockRadius);
            foreach (GlassPanel capsule in DDayItemsPanel.Children.OfType<GlassPanel>())
                ApplyPanelLighting(capsule, settings, settings.DDayPanelOpacity, settings.DDayPanelDepth, GlassPanel.ScheduleRadius);
        }

        internal void RestoreAppearance()
        {
            if (previewOriginal == null) return;
            settings = previewOriginal;
            previewOriginal = null;
            // Restore geometry after the old minimum constraint has been recomputed.
            ApplySettings(false, false);
            ApplyWindowSize(previewOriginalWidth);
        }

        internal void PreviewGlassRefraction(string color, string mode)
        {
            if (previewOriginal == null)
            {
                previewOriginal = settings;
                previewOriginalWidth = Width;
                settings = settings.Clone();
            }
            settings.GlassRefractionColor = GlassRefraction.NormalizeColor(color);
            settings.GlassRefractionMode = GlassRefraction.NormalizeMode(mode);
            ApplyPanelLighting(GlassSurface, settings, settings.PanelOpacity, settings.PanelDepth, GlassPanel.BackgroundRadius, true);
            ApplyPanelLighting(ClockCard, settings, settings.ClockPanelOpacity, settings.ClockPanelDepth, GlassPanel.ClockRadius);
            foreach (GlassPanel capsule in DDayItemsPanel.Children.OfType<GlassPanel>())
                ApplyPanelLighting(capsule, settings, settings.DDayPanelOpacity, settings.DDayPanelDepth, GlassPanel.ScheduleRadius);
        }

        private static void ApplyPanelLighting(GlassPanel panel, AppSettings value, double opacity, int depth, double referenceRadius, bool background = false)
        {
            if (panel == null) return;
            panel.ShowWhiteOutline = value.ShowPanelOutline;
            panel.SetLighting(LiquidGlassTheme.ParseColor(value.GlassLightColor, Colors.LightBlue),
                value.GlassLightDirection, opacity, depth, referenceRadius, background,
                LiquidGlassTheme.ParseColor(value.GlassRefractionColor, Color.FromRgb(126, 193, 238)), value.GlassRefractionMode,
                background ? 1.0 : referenceRadius == GlassPanel.ClockRadius ? .65 : .45);
        }

        private void ApplySurfaceSettings(AppSettings value)
        {
            HoverOutline.IsEffectEnabled = value.ShowHoverReflection;
            displaySettings = TextColorModeService.Resolve(value);
            Opacity = 1.0;
            GlassSurface.Background = LiquidGlassTheme.CreatePanelBrush(value);
            GlassSurface.BorderBrush = Brushes.Transparent;
            ApplyPanelLighting(GlassSurface, value, value.PanelOpacity, value.PanelDepth, GlassPanel.BackgroundRadius, true);
            ClockCard.Background = LiquidGlassTheme.CreateClockCardBrush(value);
            ClockCard.BorderBrush = Brushes.Transparent;
            ApplyPanelLighting(ClockCard, value, value.ClockPanelOpacity, value.ClockPanelDepth, GlassPanel.ClockRadius);
            CalendarHost.Background = Brushes.Transparent;
            ContentDivider.Background = LiquidGlassTheme.CreateCardBorder(value.PanelOpacity);
            TimeText.Opacity = DateText.Opacity = LunarDateText.Opacity = CalendarView.Opacity = value.TextOpacity;
            DDayItemsPanel.Opacity = 1;
            foreach (Border capsule in DDayItemsPanel.Children.OfType<Border>())
            {
                capsule.Background = LiquidGlassTheme.CreateScheduleCapsuleBrush(value);
                capsule.BorderBrush = Brushes.Transparent;
                ApplyPanelLighting(capsule as GlassPanel, value, value.DDayPanelOpacity, value.DDayPanelDepth, GlassPanel.ScheduleRadius);
                if (capsule.Child != null) capsule.Child.Opacity = value.TextOpacity;
            }
            ApplyTextColors();
        }

        private void ApplyTextColors()
        {
            TimeText.Foreground = BrushFrom(displaySettings.ColorTime, Colors.White);
            DateText.Foreground = LunarDateText.Foreground = BrushFrom(displaySettings.ColorDate, Colors.White);
            foreach (Border capsule in DDayItemsPanel.Children.OfType<Border>())
            {
                Grid row = capsule.Child as Grid;
                if (row == null) continue;
                row.Children.OfType<TextBlock>().First().Foreground = BrushFrom(displaySettings.ColorDDayTitle, Colors.White);
                StackPanel right = row.Children.OfType<StackPanel>().First();
                ((TextBlock)right.Children[0]).Foreground = BrushFrom(displaySettings.ColorDDayCount, Colors.White);
                ((TextBlock)right.Children[1]).Foreground = BrushFrom(displaySettings.ColorDDayDate, Colors.White);
            }
            CalendarView.ApplyColors(BrushFrom(displaySettings.ColorCalendar, Colors.White),
                LiquidGlassTheme.ParseColor(displaySettings.ColorDDayCount, Colors.LightBlue));
            UpdateDDayHighlights(DateTime.Today);
        }

        private void UpdateDDayHighlights(DateTime today)
        {
            Brush normalPanel = LiquidGlassTheme.CreateScheduleCapsuleBrush(settings);
            Brush normalTitle = BrushFrom(displaySettings.ColorDDayTitle, Colors.White);
            Brush upcomingPanel = LiquidGlassTheme.CreateUpcomingCapsuleBrush(settings);
            Brush upcomingTitle = BrushFrom(DDayHighlightPolicy.Title(settings), Colors.White);
            foreach (Border capsule in DDayItemsPanel.Children.OfType<Border>())
            {
                Grid row = capsule.Child as Grid;
                DDayItem item = row == null ? null : row.Tag as DDayItem;
                if (item == null) continue;
                bool upcoming = DDayHighlightPolicy.IsUpcoming(settings, item.Date, today);
                capsule.Background = upcoming ? upcomingPanel : normalPanel;
                TextBlock title = row.Children.OfType<TextBlock>().FirstOrDefault();
                if (title != null) title.Foreground = upcoming ? upcomingTitle : normalTitle;
            }
        }

        private double MeasureScheduleRowHeight(double scale = 1)
        {
            TextBlock title = new TextBlock(), count = new TextBlock(), detail = new TextBlock();
            SetTextStyle(title, settings.FontDDayTitle, settings.SizeDDayTitle, displaySettings.ColorDDayTitle, ParseWeight(settings.WeightDDayTitle));
            SetTextStyle(count, settings.FontDDayCount, settings.SizeDDayCount, displaySettings.ColorDDayCount, ParseWeight(settings.WeightDDayCount));
            SetTextStyle(detail, settings.FontDDayDate, settings.SizeDDayDate, displaySettings.ColorDDayDate, ParseWeight(settings.WeightDDayDate));
            title.FontSize *= scale; count.FontSize *= scale; detail.FontSize *= scale;
            Size available = new Size(double.PositiveInfinity, double.PositiveInfinity);
            count.Text = "D-Day D+8888888"; count.Measure(available);
            double titleHeight = 0, detailHeight = 0;
            foreach (DDayItem item in settings.Items)
            {
                title.Text = item.Title; title.Measure(available);
                detail.Text = FormatDate(item.Date) + " (" + Weekday(item.Date) + ")"; detail.Measure(available);
                titleHeight = Math.Max(titleHeight, title.DesiredSize.Height);
                detailHeight = Math.Max(detailHeight, detail.DesiredSize.Height);
            }
            return Math.Ceiling(Math.Max(titleHeight, count.DesiredSize.Height + detailHeight) + 8 * scale);
        }

        private void RebuildDDayItems()
        {
            DDayItemsPanel.Children.Clear();
            for (int slot = 0; slot < ScheduleCarousel.VisibleCount(settings.Items.Count, settings.VisibleDDayCount); slot++)
            {
                DDayItem item = settings.Items[ScheduleCarousel.Wrap(carousel.Index + slot, settings.Items.Count)];
                Grid row = new Grid { Tag = item, Opacity = settings.TextOpacity };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock title = new TextBlock
                {
                    Text = item.Title,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = item.Title,
                    Margin = new Thickness(0, 0, 12, 0),
                    Tag = "title"
                };
                SetTextStyle(title, settings.FontDDayTitle, settings.SizeDDayTitle,
                    displaySettings.ColorDDayTitle, ParseWeight(settings.WeightDDayTitle));
                row.Children.Add(title);

                StackPanel right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
                TextBlock count = new TextBlock { TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Margin = new Thickness(0), Tag = item };
                SetTextStyle(count, settings.FontDDayCount, settings.SizeDDayCount,
                    displaySettings.ColorDDayCount, ParseWeight(settings.WeightDDayCount));
                TextBlock detail = new TextBlock { TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Margin = new Thickness(0), Tag = "detail" };
                SetTextStyle(detail, settings.FontDDayDate, settings.SizeDDayDate,
                    displaySettings.ColorDDayDate, ParseWeight(settings.WeightDDayDate));
                right.Children.Add(count);
                right.Children.Add(detail);
                Grid.SetColumn(right, 1);
                row.Children.Add(right);
                GlassPanel capsule = new GlassPanel
                {
                    Child = row,
                    Height = rowHeight,
                    Margin = new Thickness(2, 2, 2, 2),
                    Padding = SchedulePadding(1),
                    CornerRadius = new CornerRadius(GlassPanel.ScheduleRadius),
                    BorderThickness = new Thickness(0.8),
                    Background = LiquidGlassTheme.CreateScheduleCapsuleBrush(settings),
                    BorderBrush = Brushes.Transparent
                };
                ApplyPanelLighting(capsule, settings, settings.DDayPanelOpacity, settings.DDayPanelDepth, GlassPanel.ScheduleRadius);
                DDayItemsPanel.Children.Add(capsule);
            }
        }

        private void UpdateClockAndCounts(bool force)
        {
            DateTime now = DateTime.Now;
            if (settings.ClockMode == "stopwatch")
            {
                TimeText.Period = string.Empty;
                TimeText.Text = stopwatch.Text;
            }
            else if (settings.TimeFormat == "12h")
            {
                string marker = settings.DayFormat == "kor" ? (now.Hour < 12 ? "오전" : "오후") : now.ToString("tt", CultureInfo.InvariantCulture);
                TimeText.Period = marker;
                TimeText.Text = now.ToString(settings.ShowSeconds ? "hh:mm:ss" : "hh:mm", CultureInfo.InvariantCulture);
            }
            else
            {
                TimeText.Period = string.Empty;
                TimeText.Text = now.ToString(settings.ShowSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
            }

            if (!force && lastCountDate == now.Date) return;
            DateText.Text = FormatDate(now) + " (" + Weekday(now) + ")";
            lastCountDate = now.Date;
            CalendarView.RefreshToday();
            if (!force && IsLoaded) RefreshCalendarHolidays();
            if (settings.ShowLunarDate)
            {
                string lunar;
                bool english = settings.DayFormat == "eng";
                LunarDateText.Text = LunarDateService.TryFormat(now.Date, out lunar, english, settings.DateFormat) ? lunar :
                    (english ? "Lunar: outside supported range" : "음력 지원 범위 밖");
            }
            UpdateDDayCounts(now.Date);
        }

        private void UpdateDDayCounts(DateTime today)
        {
            UpdateDDayHighlights(today);
            foreach (Border capsule in DDayItemsPanel.Children.OfType<Border>())
            {
                Grid row = capsule.Child as Grid;
                if (row == null) continue;
                StackPanel right = row.Children.OfType<StackPanel>().FirstOrDefault();
                if (right == null || right.Children.Count < 2) continue;
                DDayItem item = row.Tag as DDayItem;
                if (item == null) continue;
                int days = (item.Date.Date - today).Days;
                ((TextBlock)right.Children[0]).Text = days > 0 ? "D-" + days : days < 0 ? "D+" + Math.Abs(days) : "D-Day";
                ((TextBlock)right.Children[1]).Text = FormatDate(item.Date) + " (" + Weekday(item.Date) + ")";
            }
        }

        private void DDayArea_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (settings.Items.Count <= settings.VisibleDDayCount) return;
            e.Handled = true;
            if (carousel.Wheel(e.Delta, settings.Items.Count)) AnimateCarousel(e.Delta < 0 ? 1 : -1);
        }

        private void DDayArea_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.PageUp) { MoveCarousel(-1); e.Handled = true; }
            if (e.Key == Key.Down || e.Key == Key.PageDown) { MoveCarousel(1); e.Handled = true; }
        }
        private void MoveCarousel(int direction)
        {
            if (settings.Items.Count <= settings.VisibleDDayCount) return;
            if (carousel.Move(direction, settings.Items.Count)) AnimateCarousel(direction);
        }
        private void AnimateCarousel(int direction)
        {
            RebuildDDayItems();
            UpdateDDayCounts(DateTime.Today);
            ApplyWindowSize(Width);
            CarouselSlide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(direction * 18 * currentScale, 0, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            AppServices.Log.Info("dday.carousel", "index=" + carousel.Index + ", count=" + settings.Items.Count);
        }

        private void StopwatchToggle_OnClick(object sender, RoutedEventArgs e)
        {
            stopwatch.Toggle();
            UpdateTimerInterval();
            UpdateClockAndCounts(false);
            AppServices.Log.Info("stopwatch.toggle", "running=" + stopwatch.IsRunning);
        }
        private void StopwatchReset_OnClick(object sender, RoutedEventArgs e)
        {
            stopwatch.Reset();
            UpdateTimerInterval();
            UpdateClockAndCounts(false);
            AppServices.Log.Info("stopwatch.reset", "reset=true");
        }
        private void UpdateTimerInterval()
        {
            StopwatchToggle.Content = stopwatch.IsRunning ? "일시정지" : "시작";
            if (clockTimer != null) clockTimer.Interval = ClockSchedule.NextDelay(DateTime.Now,
                settings.ClockMode == "stopwatch" && stopwatch.IsRunning);
        }

        private string FormatDate(DateTime date)
        {
            return DateDisplayFormat.Format(date, settings.DateFormat);
        }

        private string Weekday(DateTime date)
        {
            if (settings.DayFormat == "eng") return date.ToString("ddd", CultureInfo.InvariantCulture);
            string[] days = { "일", "월", "화", "수", "목", "금", "토" };
            return days[(int)date.DayOfWeek];
        }

        private void UpdateCalendarMarks()
        {
            CalendarView.SetSchedules(settings.Items);
        }

        private void SetCalendarPublicData(string status)
        {
            CalendarView.SetPublicDates(settings.ShowKoreanHolidays && settings.ShowCalendar
                    ? AppServices.Holidays.Snapshot() : new System.Collections.Generic.List<HolidayEntry>(),
                settings.ShowSolarTerms && settings.ShowCalendar
                    ? AppServices.SolarTerms.Snapshot() : new System.Collections.Generic.List<HolidayEntry>(), status);
        }

        private async void RefreshCalendarHolidays()
        {
            if (quitting) return;
            int request = ++holidayRequestVersion;
            if ((!settings.ShowKoreanHolidays && !settings.ShowSolarTerms) || !settings.ShowCalendar)
            {
                SetCalendarPublicData(string.Empty);
                return;
            }
            bool english = settings.DayFormat == "eng";
            var sources = new System.Collections.Generic.List<KoreanHolidayService>();
            if (settings.ShowKoreanHolidays) sources.Add(AppServices.Holidays);
            if (settings.ShowSolarTerms) sources.Add(AppServices.SolarTerms);
            int[] years = CalendarMonth.GetDays(CalendarView.DisplayDate,
                settings.WeekStart == "Monday" ? DayOfWeek.Monday : DayOfWeek.Sunday)
                .Where(x => x.HasValue).Select(x => x.Value.Year).Distinct().ToArray();
            try
            {
                foreach (var source in sources)
                    foreach (int year in years) source.LoadCached(year);
                SetCalendarPublicData(english ? "Checking calendar data…" : "달력 자료 확인 중…");
                string key = AppServices.HolidayKeys.Read();
                var statuses = new System.Collections.Generic.List<string>();
                foreach (int year in years)
                {
                    if (request != holidayRequestVersion || quitting) return;
                    var results = await System.Threading.Tasks.Task.WhenAll(sources.Select(source => source.RefreshAsync(year, key, false)));
                    if (request != holidayRequestVersion || quitting) return;
                    statuses.AddRange(results.Select(result => english ? result.EnglishMessage : result.Message));
                }
                SetCalendarPublicData(string.Join("\n", statuses));
            }
            catch (Exception)
            {
                // HTTP exception text can include the API key. Keep the existing validated data.
                if (request == holidayRequestVersion && !quitting)
                    SetCalendarPublicData(english ? "Calendar data unavailable" : "달력 자료 확인 실패");
            }
        }

        private void BuildTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon
            {
                Text = "D-Day 3",
                Visible = true
            };
            try
            {
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                trayIcon.Icon = DrawingIcon.ExtractAssociatedIcon(executable);
            }
            catch { }

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false };
            menu.Items.Add("보이기/숨기기", null, delegate { Dispatcher.Invoke(ToggleVisibility); });
            menu.Items.Add("주 모니터로 가져오기", null, delegate { Dispatcher.Invoke(BringToPrimary); });
            menu.Items.Add("격자에 맞추기", null, delegate { Dispatcher.Invoke(SnapNow); });
            menu.Items.Add("설정", null, delegate { Dispatcher.Invoke(delegate { OpenSettings(null); }); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("정보", null, delegate { Dispatcher.Invoke(ShowAbout); });
            menu.Items.Add("종료", null, delegate { Dispatcher.Invoke(QuitApplication); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.MouseClick += delegate(object sender, Forms.MouseEventArgs args)
            {
                if (args.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ShowAndActivate);
            };
            trayIcon.DoubleClick += delegate { Dispatcher.Invoke(delegate { OpenSettings(null); }); };
        }

        private void BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu
            {
                Background = (Brush)Application.Current.Resources["WindowVeilBrush"],
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
            };
            AddMenuItem(menu, "설정 편집", delegate { OpenSettings(null); });
            AddMenuItem(menu, "주 모니터로 가져오기", BringToPrimary);
            AddMenuItem(menu, "격자에 맞추기", SnapNow);
            menu.Items.Add(new Separator());
            AddMenuItem(menu, settings.ShowCalendar ? "달력 숨기기" : "달력 표시", delegate
            {
                settings.ShowCalendar = !settings.ShowCalendar;
                ApplySettings();
                SaveState(true);
                BuildContextMenu();
            });
            menu.Items.Add(new Separator());
            AddMenuItem(menu, "정보", ShowAbout);
            AddMenuItem(menu, "종료", QuitApplication);
            ContextMenu = menu;
        }

        private static void AddMenuItem(ContextMenu menu, string text, Action action)
        {
            MenuItem item = new MenuItem
            {
                Header = text,
                Style = (Style)Application.Current.Resources["TextOnlyMenuItemStyle"]
            };
            item.Click += delegate { action(); };
            menu.Items.Add(item);
        }

        private void OpenSettings(DateTime? seedDate)
        {
            SyncGeometry();
            SettingsWindow dialog = new SettingsWindow(settings.Clone(), configuration, seedDate)
            {
                Owner = this
            };
            AppServices.Log.Info("settings.open", "seedDate=" + seedDate.HasValue);
            if (dialog.ShowDialog() != true) return;

            previewOriginal = null;
            settings = dialog.ResultSettings;
            StartupResult startup = AppServices.Startup.SetEnabled(settings.AutoStart, true);
            if (!startup.Success)
            {
                MessageBox.Show(this, startup.Message, "시작프로그램 설정",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ApplySettings(true);
            SaveState(true);
            BuildContextMenu();
            AppServices.Log.Info("settings.applied", "items=" + settings.Items.Count + ", visibleLimit=" + settings.VisibleDDayCount + ", textMode=" + settings.TextColorMode + ", weekStart=" + settings.WeekStart);
        }

        private void BringToPrimary()
        {
            Forms.Screen screen = Forms.Screen.PrimaryScreen;
            if (screen == null) return;
            Rect work = DevicePixelsToDip(screen.WorkingArea);
            Left = work.Left + Math.Max(0, (work.Width - ActualWidth) / 2.0);
            Top = work.Top + Math.Max(0, (work.Height - ActualHeight) / 2.0);
            ShowAndActivate();
            SaveState(true);
            AppServices.Log.Info("window.bring-primary", GeometrySummary());
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(3500, "위젯 위치 복구",
                    "위젯을 주 모니터 중앙으로 옮겼습니다.", Forms.ToolTipIcon.Info);
            }
        }

        private Rect DevicePixelsToDip(DrawingRectangle rectangle)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix transform = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            Point topLeft = transform.Transform(new Point(rectangle.Left, rectangle.Top));
            Point bottomRight = transform.Transform(new Point(rectangle.Right, rectangle.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private void ToggleVisibility()
        {
            if (IsVisible) Hide(); else ShowAndActivate();
            AppServices.Log.Info("window.visibility", "visible=" + IsVisible);
        }

        private void ShowAndActivate()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = settings.Topmost;
        }

        private void ShowAbout()
        {
            AboutWindow dialog = new AboutWindow { Owner = this };
            dialog.ShowDialog();
        }

        private void ShowConfigurationNotice()
        {
            if (initialLoadResult == null) return;
            string status = initialLoadResult.Status;
            if (status == "backup_recovered" || status == "backup_recovered_readonly" ||
                status == "partial" || status == "defaults_after_error")
            {
                Forms.ToolTipIcon icon = status == "partial" ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info;
                trayIcon.ShowBalloonTip(6000, "D-Day 3 설정 확인", initialLoadResult.Message, icon);
            }
            initialLoadResult = null;
        }

        private void SaveState(bool userInitiated)
        {
            SyncGeometry();
            ConfigSaveResult result = configuration.Save(previewOriginal ?? settings, userInitiated);
            if (!result.Success && !result.Skipped && userInitiated)
            {
                MessageBox.Show(this, result.Message, "설정 저장 실패",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SyncGeometry()
        {
            if (previewOriginal != null) return;
            settings.X = (int)Math.Round(Left);
            settings.Y = (int)Math.Round(Top);
            settings.Width = (int)Math.Round(ActualWidth > 0 ? ActualWidth : Width);
            settings.Height = (int)Math.Round(ActualHeight > 0 ? ActualHeight : Height);
        }

        private string GeometrySummary()
        {
            return "x=" + Math.Round(Left) + ", y=" + Math.Round(Top) +
                ", w=" + Math.Round(ActualWidth) + ", h=" + Math.Round(ActualHeight);
        }

        private void QuitApplication()
        {
            quitting = true;
            AppServices.IcsCalendars.Changed -= IcsFiles_OnChanged;
            if (calendarRequest != null) calendarRequest.Cancel();
            SaveState(false);
            clockTimer.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            Close();
            Application.Current.Shutdown(0);
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!quitting)
            {
                e.Cancel = true;
                Hide();
                SaveState(false);
            }
        }

        private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInteractiveSource(e.OriginalSource as DependencyObject)) return;
            if (e.ClickCount == 2)
            {
                OpenSettings(null);
                e.Handled = true;
                return;
            }
            if (e.OriginalSource == ResizeGrip) return;
            DDayArea.Focus();
            try
            {
                DragMove();
                if (settings.SnapToGrid) SnapPosition();
                SaveState(false);
                AppServices.Log.Info("window.move.completed", GeometrySummary());
            }
            catch (InvalidOperationException) { }
        }

        private void ResizeGrip_OnDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double nextWidth = WidgetLayoutPolicy.ResizeWidth(Width, e.HorizontalChange, e.VerticalChange,
                designHeight / designWidth, MinWidth);
            ApplyWindowSize(nextWidth);
        }

        private void ResizeGrip_OnDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SaveState(false);
            AppServices.Log.Info("window.resize.completed", GeometrySummary() +
                ", scale=" + WidgetLayoutPolicy.Scale(Width, Height, designWidth, designHeight).ToString("0.000", CultureInfo.InvariantCulture));
        }

        private void UpdateDesignSize(double? preservedScale = null)
        {
            if (measuringLayout || settings == null) return;
            measuringLayout = true;
            try
            {
                // Measure at reference sizes, then re-layout at final font sizes without a scale transform.
                ApplyLayoutScale(1);
                string pattern = settings.ClockMode == "stopwatch" ? "###:##:##.#" : (settings.ShowSeconds ? "##:##:##" : "##:##");
                string[] periods = settings.ClockMode == "stopwatch" || settings.TimeFormat != "12h" ? new[] { "" } :
                    settings.DayFormat == "kor" ? new[] { "오전", "오후" } : new[] { "AM", "PM" };
                designWidth = Math.Max(350, TimeText.MeasureReferenceWidth(pattern, periods) + 80);
                if (settings.ShowCalendar) designWidth = Math.Max(designWidth, CalendarView.MinWidth + 56);
                TextBlock countSample = new TextBlock();
                SetTextStyle(countSample, settings.FontDDayCount, settings.SizeDDayCount, displaySettings.ColorDDayCount, ParseWeight(settings.WeightDDayCount));
                countSample.Text = "D+8888888";
                countSample.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                TextBlock detailSample = new TextBlock();
                SetTextStyle(detailSample, settings.FontDDayDate, settings.SizeDDayDate, displaySettings.ColorDDayDate, ParseWeight(settings.WeightDDayDate));
                double detailWidth = 0;
                foreach (DDayItem item in settings.Items)
                {
                    detailSample.Text = FormatDate(item.Date) + " (" + Weekday(item.Date) + ")";
                    detailSample.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    detailWidth = Math.Max(detailWidth, detailSample.DesiredSize.Width);
                }
                designWidth = Math.Max(designWidth, WidgetLayoutPolicy.ScheduleMinimumWidth(
                    Math.Max(countSample.DesiredSize.Width, detailWidth), settings.SizeDDayTitle));
                DateText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                LunarDateText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                designWidth = Math.Max(designWidth, Math.Max(DateText.DesiredSize.Width, LunarDateText.DesiredSize.Width) + 64);
                Root.Width = Math.Ceiling(designWidth);
                designWidth = Root.Width;
                Root.Height = double.NaN;
                Root.Measure(new Size(designWidth, double.PositiveInfinity));
                designHeight = Math.Ceiling(Math.Max(100, Root.DesiredSize.Height));
                MinWidth = WidgetLayoutPolicy.MinimumReadableWidth(settings, designWidth);
                MinHeight = 64;
            }
            finally { measuringLayout = false; }
            ApplyWindowSize(preservedScale.HasValue ? designWidth * preservedScale.Value : Width);
        }

        private void ApplyWindowSize(double width)
        {
            if (measuringLayout) return;
            measuringLayout = true;
            try
            {
                Width = Math.Max(MinWidth, width);
                ApplyLayoutScale(Width / designWidth);
                Root.Width = Width;
                Root.Height = double.NaN;
                Root.Measure(new Size(Width, double.PositiveInfinity));
                // Measure final hinted glyphs and rounded rows; never crop to the ideal scaled height.
                Height = Math.Max(64, Math.Ceiling(Root.DesiredSize.Height) + 2);
            }
            finally { measuringLayout = false; }
        }

        private void ApplyLayoutScale(double scale)
        {
            currentScale = scale;
            SurfaceInset.Margin = new Thickness(6 * scale);
            ContentLayout.Margin = new Thickness(14 * scale, 12 * scale, 14 * scale, 13 * scale);
            GlassSurface.CornerRadius = new CornerRadius(GlassPanel.BackgroundRadius * scale);
            GlassSurface.BorderThickness = new Thickness(0.8 * scale);
            ClockCard.CornerRadius = new CornerRadius(GlassPanel.ClockRadius * scale);
            ClockCard.Padding = new Thickness(9 * scale, 4 * scale, 9 * scale, 7 * scale);
            ClockCard.BorderThickness = new Thickness(0.8 * scale);
            ContentDivider.Margin = new Thickness(8 * scale, 8 * scale, 8 * scale, 6 * scale);
            ContentDivider.Height = Math.Max(1, scale);
            TimeText.FontSize = settings.SizeTime * 96.0 / 72.0 * scale;
            TimeText.PeriodFontSize = Math.Max(9, settings.SizeTime * 96.0 / 72.0 * 0.34) * scale;
            DateText.FontSize = LunarDateText.FontSize = settings.SizeDate * 96.0 / 72.0 * scale;
            LunarDateText.Margin = new Thickness(0, 3 * scale, 0, 0);
            StopwatchControls.Margin = new Thickness(0, 6 * scale, 0, 0);
            foreach (Button button in StopwatchControls.Children.OfType<Button>())
            {
                button.FontSize = 12 * scale; button.Margin = new Thickness(4 * scale);
                button.Padding = new Thickness(12 * scale, 3 * scale, 12 * scale, 3 * scale);
            }
            double finalRowHeight = MeasureScheduleRowHeight(scale);
            foreach (Border capsule in DDayItemsPanel.Children.OfType<Border>())
            {
                Grid row = capsule.Child as Grid;
                if (row == null) continue;
                capsule.Height = finalRowHeight;
                capsule.Margin = new Thickness(2 * scale);
                capsule.Padding = SchedulePadding(scale);
                capsule.CornerRadius = new CornerRadius(GlassPanel.ScheduleRadius * scale);
                capsule.BorderThickness = new Thickness(0.8 * scale);
                TextBlock title = row.Children.OfType<TextBlock>().First();
                title.FontSize = settings.SizeDDayTitle * 96.0 / 72.0 * scale;
                title.Margin = new Thickness(0, 0, 12 * scale, 0);
                StackPanel right = row.Children.OfType<StackPanel>().First();
                ((TextBlock)right.Children[0]).FontSize = settings.SizeDDayCount * 96.0 / 72.0 * scale;
                ((TextBlock)right.Children[1]).FontSize = settings.SizeDDayDate * 96.0 / 72.0 * scale;
            }
            CalendarHost.Margin = new Thickness(0, 7 * scale, 0, 0);
            CalendarView.Margin = new Thickness(0);
            CalendarView.ApplyScale(scale);
        }

        private static Thickness SchedulePadding(double scale)
        {
            // Quantize once, then use the same inset at both ends of every capsule.
            double inset = Math.Round(WidgetLayoutPolicy.ScheduleSidePadding * scale);
            return new Thickness(inset, 0, inset, 0);
        }

        private void SnapNow()
        {
            SnapPosition();
            SaveState(true);
        }

        private void SnapPosition()
        {
            // Only a user drag/menu action calls this; startup and monitor changes retain coordinates.
            Forms.Screen screen = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            Rect work = DevicePixelsToDip(screen.WorkingArea);
            Left = WidgetLayoutPolicy.Snap(Left, work.Left, settings.GridSize);
            Top = WidgetLayoutPolicy.Snap(Top, work.Top, settings.GridSize);
            AppServices.Log.Info("window.grid.snap", "spacing=" + settings.GridSize + ", " + GeometrySummary());
        }

        private static bool IsInteractiveSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase || source is GlassCalendar ||
                    source is System.Windows.Controls.Primitives.Thumb || source is System.Windows.Controls.Primitives.ScrollBar)
                    return true;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return false;
        }


        private void CalendarView_OnDateActivated(object sender, CalendarDateEventArgs e)
        {
            if (AppServices.IcsCalendars.Enabled) OpenCalendarDay(e.Date);
            else OpenSettings(e.Date);
        }

        private void IcsFiles_OnChanged(object sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess()) RefreshIcsCalendar(true);
            else Dispatcher.BeginInvoke(new Action(() => RefreshIcsCalendar(true)));
        }

        private async void RefreshIcsCalendar(bool clear = false)
        {
            if (quitting) return;
            if (calendarRequest != null) { calendarRequest.Cancel(); calendarRequest.Dispose(); calendarRequest = null; }
            var service = AppServices.IcsCalendars;
            CalendarView.OpenEventsOnSingleClick = service.Enabled;
            if (clear || !service.Enabled)
            {
                calendarMonthEvents.Clear(); CalendarView.SetCalendarEvents(calendarMonthEvents, "");
            }
            if (!service.Enabled || !settings.ShowCalendar) return;
            var range = CalendarMonth.GetDays(CalendarView.DisplayDate, settings.WeekStart == "Monday" ? DayOfWeek.Monday : DayOfWeek.Sunday)
                .Where(d => d.HasValue).Select(d => d.Value).ToArray();
            var request = new CancellationTokenSource(); calendarRequest = request;
            var token = request.Token;
            try
            {
                DateTime last = range.Last();
                var result = await service.Events(range.First(), last == DateTime.MaxValue.Date ? DateTime.MaxValue : last.AddDays(1), token);
                if (token.IsCancellationRequested || quitting) return;
                calendarMonthEvents = result;
                CalendarView.SetCalendarEvents(calendarMonthEvents, "");
            }
            catch (OperationCanceledException)
            {
                if (!token.IsCancellationRequested && !quitting)
                    CalendarView.SetCalendarEvents(calendarMonthEvents, "일정 표시가 취소되었습니다.");
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && !quitting)
                    CalendarView.SetCalendarEvents(calendarMonthEvents, "일정 파일을 표시하지 못했습니다. " + ex.Message);
            }
        }

        private void OpenCalendarDay(DateTime date)
        {
            if (calendarDialogOpen) return;
            calendarDialogOpen = true;
            try
            {
                if (!AppServices.IcsCalendars.Enabled)
                {
                    new IcsCalendarWindow(AppServices.IcsCalendars) { Owner = this }.ShowDialog();
                    if (!AppServices.IcsCalendars.Enabled) return;
                }
                var dialog = new CalendarDayWindow(AppServices.IcsCalendars, date, settings.Items, settings.DateFormat) { Owner = this };
                if (dialog.ShowDialog() != true) return;
                if (dialog.ManualEntryRequested) { OpenSettings(date); return; }
                SyncGeometry();
                var next = settings.Clone();
                int firstImported = next.Items.Count;
                foreach (var item in dialog.SelectedEvents)
                    if (!next.Items.Any(existing => existing.CalendarEventKey == item.Key))
                        next.Items.Add(new DDayItem { Title = item.Title, Date = item.Start.Date, CalendarEventKey = item.Key });
                if (next.Items.Count == firstImported) return;
                ConfigSaveResult saved = configuration.Save(next, true);
                if (!saved.Success)
                {
                    MessageBox.Show(this, saved.Message, "D-Day 등록 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                settings = next; ApplySettings(true); BuildContextMenu();
                carousel.Move(firstImported, settings.Items.Count);
                RebuildDDayItems(); UpdateClockAndCounts(true);
            }
            finally { calendarDialogOpen = false; }
        }

        private void CalendarView_OnDateContextRequested(object sender, CalendarDateEventArgs e)
        {
            DateTime date = e.Date;
            ContextMenu menu = new ContextMenu
            {
                Background = (Brush)Application.Current.Resources["WindowVeilBrush"],
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
            };
            AddMenuItem(menu, "새 D-Day 추가 (" + date.ToString("yyyy-MM-dd") + ")", delegate { OpenSettings(date); });
            AddMenuItem(menu, "일정 파일에서 D-Day 추가…", delegate { OpenCalendarDay(date); });
            AddMenuItem(menu, "캘린더 파일·URL 관리…", delegate { new IcsCalendarWindow(AppServices.IcsCalendars) { Owner = this }.ShowDialog(); });
            menu.Items.Add(new Separator());
            AddMenuItem(menu, "Google 캘린더 열기", delegate
            {
                StartupService.OpenUrl("https://calendar.google.com/calendar/u/0/r/day/" +
                    date.Year + "/" + date.Month + "/" + date.Day);
            });
            AddMenuItem(menu, "Outlook 캘린더 열기", delegate
            {
                StartupService.OpenUrl("https://outlook.live.com/calendar/");
            });
            menu.IsOpen = true;
        }

        private static void SetTextStyle(TextBlock text, string family, double size, string color, FontWeight weight)
        {
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
            text.UseLayoutRounding = true;
            text.SnapsToDevicePixels = true;
            text.FontFamily = new FontFamily(family);
            text.FontSize = size * 96.0 / 72.0;
            text.Foreground = BrushFrom(color, Colors.White);
            text.FontWeight = weight;
        }

        private static FontWeight ParseWeight(string value)
        {
            if (string.Equals(value, "Bold", StringComparison.OrdinalIgnoreCase)) return FontWeights.Bold;
            if (string.Equals(value, "SemiBold", StringComparison.OrdinalIgnoreCase)) return FontWeights.SemiBold;
            if (string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase)) return FontWeights.Medium;
            return FontWeights.Normal;
        }

        private static Brush BrushFrom(string value, Color fallback)
        {
            return new SolidColorBrush(LiquidGlassTheme.ParseColor(value, fallback));
        }
    }
}
