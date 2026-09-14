using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation;
using DDay3.Models;
using DDay3.Services;
using Microsoft.Win32;
using DrawingColor = System.Drawing.Color;
using Forms = System.Windows.Forms;

namespace DDay3.Views
{
    public partial class SettingsWindow : Window
    {
        private sealed class DDayEditorRow
        {
            public string Title { get; set; }
            public DateTime Date { get; set; }
        }

        private sealed class TypographyEditor
        {
            internal string Key { get; set; }
            internal ComboBox Font { get; set; }
            internal ComboBox Weight { get; set; }
            internal TextBox Size { get; set; }
            internal Button Color { get; set; }
        }

        private readonly ConfigurationService configuration;
        private readonly ObservableCollection<DDayEditorRow> rows = new ObservableCollection<DDayEditorRow>();
        private readonly List<TypographyEditor> typographyEditors = new List<TypographyEditor>();
        private AppSettings working;
        private string selectedGlassColor;
        private string selectedThemeId;
        private string themeSeedColor;
        private bool isPopulating;
        private bool deleteHolidayKey;
        private bool windowClosed;
        private readonly DispatcherTimer previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        private readonly string[] fontNames = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToArray();

        internal AppSettings ResultSettings { get; private set; }

        internal SettingsWindow(AppSettings settings, ConfigurationService configuration, DateTime? seedDate)
        {
            InitializeComponent();
            this.configuration = configuration;
            working = settings;
            selectedGlassColor = settings.GlassLightColor;
            selectedThemeId = settings.ThemeId;
            themeSeedColor = settings.ThemeSeedColor;
            DDayGrid.ItemsSource = rows;
            InitializeChoiceLists();
            CreateTypographyEditors();
            CreateDepthPresets();
            previewTimer.Tick += delegate { previewTimer.Stop(); ApplyPreview(); };
            PopulateFromSettings(settings);
            UpdateHolidayKeyHint();

            if (seedDate.HasValue)
            {
                rows.Add(new DDayEditorRow { Title = "새 D-Day", Date = seedDate.Value.Date });
                SettingsTabs.SelectedIndex = 0;
            }

            foreach (DDay3.Controls.OpacityJogDial dial in new[] { OpacityDial, ClockOpacityDial, DDayOpacityDial })
                dial.ValueChanged += delegate { PreviewSurface(); };
            LightDirectionDial.ValueChanged += delegate { PreviewRelief(); };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            MainWindow owner = Owner as MainWindow;
            previewTimer.Stop();
            if (DialogResult != true && owner != null) owner.RestoreAppearance();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            windowClosed = true;
            base.OnClosed(e);
        }

        private void InitializeChoiceLists()
        {
            DDaySortCombo.Items.Add(new ComboBoxItem { Content = "날짜순 · 빠른 날짜 먼저", Tag = "date_asc" });
            DDaySortCombo.Items.Add(new ComboBoxItem { Content = "날짜순 · 늦은 날짜 먼저", Tag = "date_desc" });
            DDaySortCombo.Items.Add(new ComboBoxItem { Content = "오늘과 가까운 날순 · 과거 포함", Tag = "nearest" });
            DDaySortCombo.Items.Add(new ComboBoxItem { Content = "제목순 · 가나다 / ABC", Tag = "title" });
            DDaySortCombo.SelectedIndex = 0;
            string category = null;
            foreach (TypographyPreset preset in TypographyPreset.All)
            {
                if (preset.Category != category)
                {
                    category = preset.Category;
                    FontPresetCombo.Items.Add(new ComboBoxItem { Content = category, IsEnabled = false, FontWeight = FontWeights.SemiBold });
                }
                FontPresetCombo.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset.Id });
            }
            SelectByTag(FontPresetCombo, "ko-modern");
            TextColorModeCombo.Items.Add(new ComboBoxItem { Content = "밝은 글자 · 어두운 배경용", Tag = "light" });
            TextColorModeCombo.Items.Add(new ComboBoxItem { Content = "어두운 글자 · 밝은 배경용", Tag = "dark" });
            TextColorModeCombo.Items.Add(new ComboBoxItem { Content = "사용자 지정 색상 그대로", Tag = "custom" });
            WeekStartCombo.Items.Add(new ComboBoxItem { Content = "일요일", Tag = "Sunday" });
            WeekStartCombo.Items.Add(new ComboBoxItem { Content = "월요일", Tag = "Monday" });
            for (int count = 1; count <= 10; count++)
                VisibleDDayCountCombo.Items.Add(new ComboBoxItem { Content = count + "개", Tag = count.ToString(CultureInfo.InvariantCulture) });
            ClockModeCombo.Items.Add(new ComboBoxItem { Content = "일반 시계", Tag = "clock" });
            ClockModeCombo.Items.Add(new ComboBoxItem { Content = "초시계 (스톱워치)", Tag = "stopwatch" });
            foreach (int spacing in new[] { 8, 16, 24, 32, 48, 64 })
                GridSizeCombo.Items.Add(new ComboBoxItem { Content = spacing.ToString(CultureInfo.InvariantCulture), Tag = spacing.ToString(CultureInfo.InvariantCulture) });
            TimeFormatCombo.Items.Add(new ComboBoxItem { Content = "24시간제", Tag = "24h" });
            TimeFormatCombo.Items.Add(new ComboBoxItem { Content = "12시간제", Tag = "12h" });
            DateFormatCombo.Items.Add(new ComboBoxItem { Content = "연-월-일", Tag = "yyyy-mm-dd" });
            DateFormatCombo.Items.Add(new ComboBoxItem { Content = "월/일/연", Tag = "mm/dd/yyyy" });
            DateFormatCombo.Items.Add(new ComboBoxItem { Content = "일/월/연", Tag = "dd/mm/yyyy" });
            DayFormatCombo.Items.Add(new ComboBoxItem { Content = "한글", Tag = "kor" });
            DayFormatCombo.Items.Add(new ComboBoxItem { Content = "영문", Tag = "eng" });
        }

        private void CreateTypographyEditors()
        {
            AddTypographyEditor("time", "시간");
            AddTypographyEditor("date", "날짜·요일");
            AddTypographyEditor("dday_title", "D-Day 제목");
            AddTypographyEditor("dday_count", "D-Day 숫자");
            AddTypographyEditor("dday_date", "목표 날짜");
            AddTypographyEditor("calendar", "달력");
        }

        private void AddTypographyEditor(string key, string label)
        {
            Grid row = new Grid { Height = 36 };
            foreach (double width in new[] { 68.0, -1.0, 66.0, 88.0, 32.0 })
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
            TextBlock name = new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            ComboBox font = new ComboBox { IsTextSearchEnabled = true, MinWidth = 0, FontSize = 12,
                Padding = new Thickness(3, 2, 3, 2), Margin = new Thickness(1, 3, 3, 3), VerticalContentAlignment = VerticalAlignment.Center };
            font.ItemsSource = fontNames;
            ComboBox weight = new ComboBox { FontSize = 11, Padding = new Thickness(2, 2, 2, 2),
                Margin = new Thickness(1, 3, 3, 3), VerticalContentAlignment = VerticalAlignment.Center };
            weight.Items.Add(new ComboBoxItem { Content = "보통", Tag = "Normal" });
            weight.Items.Add(new ComboBoxItem { Content = "중간", Tag = "Medium" });
            weight.Items.Add(new ComboBoxItem { Content = "반굵게", Tag = "SemiBold" });
            weight.Items.Add(new ComboBoxItem { Content = "굵게", Tag = "Bold" });
            TextBox size = new TextBox { TextAlignment = TextAlignment.Center, FontSize = 12, Padding = new Thickness(0),
                Margin = new Thickness(1, 3, 1, 3), MaxLength = 3, VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = label + " 기준 크기(pt). ↑↓ 또는 − / +로 1pt 조절" };
            Button color = new Button { Padding = new Thickness(0), Margin = new Thickness(2, 4, 2, 4),
                MinHeight = 0, Tag = "#FFFFFF" };
            color.Click += TypographyColor_OnClick;
            font.SelectionChanged += TypographySelection_OnChanged;
            weight.SelectionChanged += TypographySelection_OnChanged;
            size.TextChanged += TypographyText_OnChanged;
            AutomationProperties.SetName(font, label + " 글꼴");
            AutomationProperties.SetName(weight, label + " 굵기");
            AutomationProperties.SetName(color, label + " 색상");
            Grid sizeControl = new Grid();
            sizeControl.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            sizeControl.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sizeControl.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            Button down = new Button { Content = "−", Padding = new Thickness(0), Margin = new Thickness(1, 3, 1, 3),
                MinHeight = 0, ToolTip = label + " 1pt 줄이기" };
            Button up = new Button { Content = "+", Padding = new Thickness(0), Margin = new Thickness(1, 3, 1, 3),
                MinHeight = 0, ToolTip = label + " 1pt 키우기" };
            AutomationProperties.SetName(size, label + " 글자 크기 pt");
            AutomationProperties.SetName(down, label + " 글자 크기 줄이기");
            AutomationProperties.SetName(up, label + " 글자 크기 키우기");
            down.Click += delegate { StepSize(size, -1); };
            up.Click += delegate { StepSize(size, 1); };
            size.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Up && e.Key != Key.Down) return;
                StepSize(size, e.Key == Key.Up ? 1 : -1); e.Handled = true;
            };
            Grid.SetColumn(size, 1); Grid.SetColumn(up, 2);
            sizeControl.Children.Add(down); sizeControl.Children.Add(size); sizeControl.Children.Add(up);
            Grid.SetColumn(font, 1); Grid.SetColumn(weight, 2); Grid.SetColumn(sizeControl, 3); Grid.SetColumn(color, 4);
            row.Children.Add(name); row.Children.Add(font); row.Children.Add(weight);
            row.Children.Add(sizeControl); row.Children.Add(color);
            Border card = new Border { Child = row, Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(0, 0, 0, 2), CornerRadius = new CornerRadius(7),
                Background = (Brush)Application.Current.Resources["GlassCardBrush"] };
            TypographyPanel.Children.Add(card);
            typographyEditors.Add(new TypographyEditor { Key = key, Font = font, Weight = weight, Size = size, Color = color });
        }

        private void PopulateFromSettings(AppSettings value)
        {
            isPopulating = true;
            try
            {
                working = value.Clone();
                rows.Clear();
                foreach (DDayItem item in working.Items)
                    rows.Add(new DDayEditorRow { Title = item.Title, Date = item.Date });

                SelectByTag(TimeFormatCombo, working.TimeFormat);
                SelectByTag(DateFormatCombo, working.DateFormat);
                SelectByTag(DayFormatCombo, working.DayFormat);
                TopmostCheck.IsChecked = working.Topmost;
                ShowCalendarCheck.IsChecked = working.ShowCalendar;
                ShowKoreanHolidaysCheck.IsChecked = working.ShowKoreanHolidays;
                ShowSolarTermsCheck.IsChecked = working.ShowSolarTerms;
                CalendarWeekendCheck.IsChecked = working.CalendarWeekendColors;
                SelectByTag(TextColorModeCombo, working.TextColorMode);
                SelectByTag(WeekStartCombo, working.WeekStart);
                SelectByTag(VisibleDDayCountCombo, working.VisibleDDayCount.ToString(CultureInfo.InvariantCulture));
                AutoStartCheck.IsChecked = working.AutoStart;
                ShowSecondsCheck.IsChecked = working.ShowSeconds;
                ShowLunarDateCheck.IsChecked = working.ShowLunarDate;
                SnapToGridCheck.IsChecked = working.SnapToGrid;
                SelectByTag(ClockModeCombo, working.ClockMode);
                string gridTag = working.GridSize.ToString(CultureInfo.InvariantCulture);
                if (!GridSizeCombo.Items.OfType<ComboBoxItem>().Any(item => Convert.ToString(item.Tag) == gridTag))
                    GridSizeCombo.Items.Add(new ComboBoxItem { Content = gridTag, Tag = gridTag });
                SelectByTag(GridSizeCombo, gridTag);
                OpacityDial.Value = working.PanelOpacity * 100.0;
                ClockOpacityDial.Value = working.ClockPanelOpacity * 100.0;
                DDayOpacityDial.Value = working.DDayPanelOpacity * 100.0;
                TextOpacitySlider.Value = working.TextOpacity * 100.0;
                GlassStrengthSlider.Value = Math.Max(10.0, working.GlassStrength * 100.0);
                selectedGlassColor = working.GlassLightColor;
                LightDirectionDial.Value = working.GlassLightDirection;
                PanelDepthSlider.Value = working.PanelDepth;
                ClockDepthSlider.Value = working.ClockPanelDepth;
                DDayDepthSlider.Value = working.DDayPanelDepth;
                selectedThemeId = working.ThemeId;
                themeSeedColor = working.ThemeSeedColor;
                UpdateGlassColorButton();

                SetTypography("time", working.FontTime, working.WeightTime, working.SizeTime, working.ColorTime);
                SetTypography("date", working.FontDate, working.WeightDate, working.SizeDate, working.ColorDate);
                SetTypography("dday_title", working.FontDDayTitle, working.WeightDDayTitle, working.SizeDDayTitle, working.ColorDDayTitle);
                SetTypography("dday_count", working.FontDDayCount, working.WeightDDayCount, working.SizeDDayCount, working.ColorDDayCount);
                SetTypography("dday_date", working.FontDDayDate, working.WeightDDayDate, working.SizeDDayDate, working.ColorDDayDate);
                SetTypography("calendar", working.FontCalendar, working.WeightCalendar, working.SizeCalendar, working.ColorCalendar);
            }
            finally
            {
                isPopulating = false;
            }
            UpdateThemeSelection();
            UpdateDepthSelection();
            PreviewSurface();
        }

        private AppSettings CollectSettings()
        {
            CommitDDayEdits();
            if (rows.Count == 0) throw new InvalidOperationException("D-Day 항목을 하나 이상 추가해 주세요.");

            AppSettings value = working.Clone();
            value.Items.Clear();
            foreach (DDayEditorRow row in rows)
            {
                string title = (row.Title ?? string.Empty).Trim();
                if (title.Length == 0) throw new InvalidOperationException("D-Day 제목이 비어 있습니다.");
                value.Items.Add(new DDayItem { Title = title, Date = row.Date.Date });
            }
            value.TimeFormat = SelectedTag(TimeFormatCombo, "24h");
            value.DateFormat = SelectedTag(DateFormatCombo, "yyyy-mm-dd");
            value.DayFormat = SelectedTag(DayFormatCombo, "kor");
            value.Topmost = TopmostCheck.IsChecked == true;
            value.ShowCalendar = ShowCalendarCheck.IsChecked == true;
            value.ShowKoreanHolidays = ShowKoreanHolidaysCheck.IsChecked == true;
            value.ShowSolarTerms = ShowSolarTermsCheck.IsChecked == true;
            value.CalendarWeekendColors = CalendarWeekendCheck.IsChecked == true;
            value.TextColorMode = SelectedTag(TextColorModeCombo, "custom");
            value.WeekStart = SelectedTag(WeekStartCombo, "Sunday");
            value.VisibleDDayCount = int.Parse(SelectedTag(VisibleDDayCountCombo, "3"), CultureInfo.InvariantCulture);
            value.AutoStart = AutoStartCheck.IsChecked == true;
            value.ShowSeconds = ShowSecondsCheck.IsChecked == true;
            value.ShowLunarDate = ShowLunarDateCheck.IsChecked == true;
            value.SnapToGrid = SnapToGridCheck.IsChecked == true;
            value.ClockMode = SelectedTag(ClockModeCombo, "clock");
            value.GridSize = int.Parse(SelectedTag(GridSizeCombo, "16"), CultureInfo.InvariantCulture);
            value.PanelOpacity = OpacityDial.Value / 100.0;
            value.ClockPanelOpacity = ClockOpacityDial.Value / 100.0;
            value.DDayPanelOpacity = DDayOpacityDial.Value / 100.0;
            value.TextOpacity = TextOpacitySlider.Value / 100.0;
            value.GlassStrength = GlassStrengthSlider.Value / 100.0;
            value.GlassLightColor = selectedGlassColor;
            value.GlassLightDirection = LightDirectionDial.Value;
            value.PanelDepth = (int)PanelDepthSlider.Value;
            value.ClockPanelDepth = (int)ClockDepthSlider.Value;
            value.DDayPanelDepth = (int)DDayDepthSlider.Value;
            value.ThemeId = selectedThemeId ?? "custom";
            value.ThemeSeedColor = themeSeedColor;

            CollectTypography(value);
            return value;
        }

        private void CollectTypography(AppSettings value)
        {
            ApplyTypography("time", (font, weight, size, color) => { value.FontTime = font; value.WeightTime = weight; value.SizeTime = size; value.ColorTime = color; });
            ApplyTypography("date", (font, weight, size, color) => { value.FontDate = font; value.WeightDate = weight; value.SizeDate = size; value.ColorDate = color; });
            ApplyTypography("dday_title", (font, weight, size, color) => { value.FontDDayTitle = font; value.WeightDDayTitle = weight; value.SizeDDayTitle = size; value.ColorDDayTitle = color; });
            ApplyTypography("dday_count", (font, weight, size, color) => { value.FontDDayCount = font; value.WeightDDayCount = weight; value.SizeDDayCount = size; value.ColorDDayCount = color; });
            ApplyTypography("dday_date", (font, weight, size, color) => { value.FontDDayDate = font; value.WeightDDayDate = weight; value.SizeDDayDate = size; value.ColorDDayDate = color; });
            ApplyTypography("calendar", (font, weight, size, color) => { value.FontCalendar = font; value.WeightCalendar = weight; value.SizeCalendar = size; value.ColorCalendar = color; });
        }

        private void ApplyTypography(string key, Action<string, string, int, string> apply)
        {
            TypographyEditor editor = typographyEditors.First(item => item.Key == key);
            int input, size;
            if (!int.TryParse(editor.Size.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out input) ||
                !TypographySize.TryNormalize(input, out size))
                throw new InvalidOperationException("글자 크기는 5~150pt 범위의 정수로 입력해 주세요.");
            string font = editor.Font.SelectedItem as string ?? editor.Font.Text;
            if (string.IsNullOrWhiteSpace(font)) font = TypographySize.DefaultFamily(key);
            apply(font, SelectedTag(editor.Weight, "Normal"), size,
                Convert.ToString(editor.Color.Tag, CultureInfo.InvariantCulture));
        }

        private static void StepSize(TextBox input, int direction)
        {
            int value;
            if (!int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) value = 12;
            input.Text = TypographySize.Step(value, direction).ToString(CultureInfo.InvariantCulture);
        }

        private void SetTypography(string key, string font, string weight, int size, string color)
        {
            TypographyEditor editor = typographyEditors.First(item => item.Key == key);
            editor.Font.SelectedItem = font;
            if (editor.Font.SelectedIndex < 0) editor.Font.Text = font;
            SelectByTag(editor.Weight, weight);
            editor.Size.Text = size.ToString(CultureInfo.InvariantCulture);
            SetColorButton(editor.Color, color);
        }

        private void AddDDay_OnClick(object sender, RoutedEventArgs e)
        {
            if (!TryCommitListEdits()) return;
            DDayEditorRow row = new DDayEditorRow { Title = "새 D-Day", Date = DateTime.Today };
            rows.Add(row);
            DDayGrid.SelectedItem = row;
            DDayGrid.ScrollIntoView(row);
            AppServices.Log.Info("settings.dday.add", "count=" + rows.Count);
        }

        private void RemoveSelectedDDay_OnClick(object sender, RoutedEventArgs e)
        {
            DDayEditorRow row = DDayGrid.SelectedItem as DDayEditorRow;
            if (row == null) return;
            if (!TryCommitListEdits()) return;
            rows.Remove(row);
            AppServices.Log.Info("settings.dday.remove", "count=" + rows.Count);
        }

        private bool TryCommitListEdits()
        {
            try { CommitDDayEdits(); return true; }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "일정 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void SortDDay_OnClick(object sender, RoutedEventArgs e)
        {
            if (!TryCommitListEdits()) return;
            object selected = DDayGrid.SelectedItem;
            var entries = rows.Select(row => new DDayItem { Title = row.Title, Date = row.Date }).ToList();
            var map = entries.Select((item, index) => new { item, row = rows[index] }).ToDictionary(x => x.item, x => x.row);
            var ordered = ScheduleOrdering.Sort(entries, SelectedTag(DDaySortCombo, "date_asc"), DateTime.Today);
            rows.Clear();
            foreach (DDayItem item in ordered) rows.Add(map[item]);
            DDayGrid.SelectedItem = selected;
        }

        private void MoveDDayUp_OnClick(object sender, RoutedEventArgs e) { MoveDDay((DDayEditorRow)((Button)sender).Tag, -1); }
        private void MoveDDayDown_OnClick(object sender, RoutedEventArgs e) { MoveDDay((DDayEditorRow)((Button)sender).Tag, 1); }

        private void MoveDDay(DDayEditorRow row, int direction)
        {
            if (!TryCommitListEdits()) return;
            int index = rows.IndexOf(row);
            int next = index + direction;
            if (index < 0 || next < 0 || next >= rows.Count) return;
            rows.Move(index, next);
            DDayGrid.SelectedItem = row;
            DDayGrid.ScrollIntoView(row);
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultSettings = CollectSettings();
                if (deleteHolidayKey) AppServices.HolidayKeys.Delete();
                else if (!string.IsNullOrWhiteSpace(HolidayKeyInput.Password)) AppServices.HolidayKeys.Write(HolidayKeyInput.Password);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "설정 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SettingsTools_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            button.ContextMenu.IsOpen = true;
        }

        private void HolidaySource_OnClick(object sender, RoutedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(HolidayData.SourcePage) { UseShellExecute = true }); }
            catch (Exception) { HolidayStatusText.Text = "브라우저를 열지 못했습니다. data.go.kr에서 한국천문연구원 특일 정보를 검색하세요."; }
        }

        private void UpdateHolidayKeyHint()
        {
            if (HolidayKeyHint == null || AppServices.HolidayKeys == null) return;
            HolidayKeyHint.Text = deleteHolidayKey ? "저장하면 기존 인증키를 삭제합니다." :
                !string.IsNullOrWhiteSpace(HolidayKeyInput.Password) ? "새 인증키 · 저장하면 암호화해 보관합니다." :
                AppServices.HolidayKeys.Read().Length > 0 ? "저장된 인증키 사용 · 변경하려면 새 키를 입력하세요." : "저장된 인증키가 없습니다.";
        }
        private void HolidayKey_OnChanged(object sender, RoutedEventArgs e)
        {
            if (HolidayKeyInput != null && HolidayKeyInput.Password.Length > 0) deleteHolidayKey = false;
            UpdateHolidayKeyHint();
        }
        private void HolidayDeleteKey_OnClick(object sender, RoutedEventArgs e)
        {
            deleteHolidayKey = true;
            HolidayKeyInput.Clear();
            UpdateHolidayKeyHint();
        }
        private async void HolidayRefresh_OnClick(object sender, RoutedEventArgs e)
        {
            var sources = new List<KoreanHolidayService>();
            if (ShowKoreanHolidaysCheck.IsChecked == true) sources.Add(AppServices.Holidays);
            if (ShowSolarTermsCheck.IsChecked == true) sources.Add(AppServices.SolarTerms);
            if (sources.Count == 0) { HolidayStatusText.Text = "표시할 공휴일 또는 24절기를 선택하세요."; return; }
            string key = HolidayKeyInput.Password;
            if (string.IsNullOrWhiteSpace(key) && !deleteHolidayKey) key = AppServices.HolidayKeys.Read();
            if (string.IsNullOrWhiteSpace(key))
            {
                HolidayStatusText.Text = "활용신청 후 발급받은 인증키를 입력하세요.";
                return;
            }
            MainWindow owner = Owner as MainWindow;
            int year = owner == null ? DateTime.Today.Year : owner.CalendarYear;
            HolidayRefreshButton.IsEnabled = false;
            HolidayStatusText.Text = year + "년 선택 자료 조회 중…";
            try
            {
                var results = await System.Threading.Tasks.Task.WhenAll(sources.Select(source => source.RefreshAsync(year, key, true)));
                if (!windowClosed) HolidayStatusText.Text = string.Join("\n", results.Select(result => result.Message));
                if (owner != null) owner.RefreshHolidayDisplay();
            }
            catch (Exception)
            {
                if (!windowClosed) HolidayStatusText.Text = "조회하지 못했습니다. 인증키와 인터넷 연결을 확인하세요.";
            }
            finally { if (!windowClosed) HolidayRefreshButton.IsEnabled = true; }
        }

        private void CommitDDayEdits()
        {
            if (!DDayGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !DDayGrid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidOperationException("편집 중인 일정의 날짜와 제목을 확인해 주세요.");
        }

        private void ResetDefaults_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBoxResult answer = MessageBox.Show(this,
                "일정 목록은 유지하고 표시·동작 설정을 기본값으로 되돌릴까요?\n저장 버튼을 누르면 확정됩니다.",
                "기본 설정 초기화", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                CommitDDayEdits();
                AppSettings draft = working.Clone();
                draft.Items.Clear();
                foreach (DDayEditorRow row in rows)
                    draft.Items.Add(new DDayItem { Title = row.Title, Date = row.Date });
                PopulateFromSettings(draft.ResetPresentation());
                AppServices.Log.Info("settings.defaults.preview", "itemsPreserved=" + rows.Count);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "일정 편집 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RestoreBackup_OnClick(object sender, RoutedEventArgs e)
        {
            ConfigLoadResult result = configuration.RestoreLatestBackup();
            if (result.Status != "backup_loaded")
            {
                MessageBox.Show(this, result.Message, "최근 백업 복원", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PopulateFromSettings(result.Settings);
            AppServices.Log.Info("settings.backup.preview", "items=" + result.Settings.Items.Count);
        }

        private void Import_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "D-Day 설정 불러오기",
                Filter = "INI 설정 파일 (*.ini)|*.ini|모든 파일 (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;
            ConfigLoadResult result = configuration.Import(dialog.FileName);
            if (result.Status != "imported")
            {
                MessageBox.Show(this, result.Message, "설정 불러오기", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PopulateFromSettings(result.Settings);
            AppServices.Log.Info("settings.import.preview", "items=" + result.Settings.Items.Count);
        }

        private void Export_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                AppSettings value = CollectSettings();
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Title = "D-Day 설정 내보내기",
                    Filter = "INI 설정 파일 (*.ini)|*.ini",
                    FileName = "dday_config_export.ini",
                    AddExtension = true
                };
                if (dialog.ShowDialog(this) != true) return;
                configuration.Export(value, dialog.FileName);
                MessageBox.Show(this, "설정을 내보냈습니다.", "설정 내보내기",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "설정 내보내기", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateDepthPresets()
        {
            foreach (GlassDepthPreset preset in GlassDepthPreset.All)
            {
                Button button = new Button { Content = preset.Name, Tag = preset, FontSize = 11,
                    Padding = new Thickness(8, 5, 8, 5), MinHeight = 28,
                    ToolTip = "전체 " + preset.Background + " / 시계 " + preset.Clock + " / D-Day " + preset.DDay };
                AutomationProperties.SetName(button, preset.Name + " 깊이 프리셋");
                button.Click += DepthPreset_OnClick;
                DepthPresetPanel.Children.Add(button);
            }
        }

        private void DepthPreset_OnClick(object sender, RoutedEventArgs e)
        {
            var preset = (GlassDepthPreset)((Button)sender).Tag;
            isPopulating = true;
            try
            {
                PanelDepthSlider.Value = preset.Background;
                ClockDepthSlider.Value = preset.Clock;
                DDayDepthSlider.Value = preset.DDay;
            }
            finally { isPopulating = false; }
            UpdateDepthSelection();
            PreviewRelief();
        }

        private void DepthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isPopulating || PanelDepthSlider == null || ClockDepthSlider == null || DDayDepthSlider == null || DepthPresetPanel == null) return;
            UpdateDepthSelection();
            PreviewRelief();
        }

        private void UpdateDepthSelection()
        {
            var match = GlassDepthPreset.Match((int)PanelDepthSlider.Value, (int)ClockDepthSlider.Value, (int)DDayDepthSlider.Value);
            foreach (Button button in DepthPresetPanel.Children.OfType<Button>())
            {
                var preset = (GlassDepthPreset)button.Tag;
                bool selected = match == preset;
                button.Content = (selected ? "✓ " : "") + preset.Name;
                button.BorderBrush = selected ? (Brush)Application.Current.Resources["AccentBrush"] : Brushes.Transparent;
                button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void PreviewRelief()
        {
            MainWindow owner = Owner as MainWindow;
            if (isPopulating || owner == null) return;
            owner.PreviewGlassRelief(LightDirectionDial.Value,
                (int)PanelDepthSlider.Value, (int)ClockDepthSlider.Value, (int)DDayDepthSlider.Value);
        }

        private void CleanDesign_OnClick(object sender, RoutedEventArgs e)
        {
            AppSettings clean = AppSettings.CreateDefault();
            ThemePalette palette = ThemePalette.Preset(clean.ThemeId);
            // Change only surface and text colors: keep editor rows, typography, geometry,
            // date/display choices and the user's unsaved edits intact.
            isPopulating = true;
            try
            {
                OpacityDial.Value = clean.PanelOpacity * 100;
                ClockOpacityDial.Value = clean.ClockPanelOpacity * 100;
                DDayOpacityDial.Value = clean.DDayPanelOpacity * 100;
                PanelDepthSlider.Value = clean.PanelDepth;
                ClockDepthSlider.Value = clean.ClockPanelDepth;
                DDayDepthSlider.Value = clean.DDayPanelDepth;
                LightDirectionDial.Value = clean.GlassLightDirection;
                TextOpacitySlider.Value = clean.TextOpacity * 100;
                GlassStrengthSlider.Value = clean.GlassStrength * 100;
                selectedGlassColor = clean.GlassLightColor;
                selectedThemeId = clean.ThemeId;
                themeSeedColor = clean.ThemeSeedColor;
                SelectByTag(TextColorModeCombo, clean.TextColorMode);
                for (int i = 0; i < typographyEditors.Count; i++) SetColorButton(typographyEditors[i].Color, palette.Colors[i]);
            }
            finally { isPopulating = false; }
            UpdateGlassColorButton();
            UpdateThemeSelection();
            UpdateDepthSelection();
            previewTimer.Stop();
            ApplyPreview();
        }

        private void LayerOpacityStep_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            var dial = button.Tag as DDay3.Controls.OpacityJogDial;
            if (dial != null) dial.Value += int.Parse(Convert.ToString(button.CommandParameter), CultureInfo.InvariantCulture);
        }

        private void GlassStrengthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GlassStrengthValue != null) GlassStrengthValue.Text = Math.Round(e.NewValue) + "%";
            PreviewSurface();
        }

        private void TextOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TextOpacityValue != null) TextOpacityValue.Text = Math.Round(e.NewValue) + "%";
            PreviewSurface();
        }

        private void PreviewSurface()
        {
            if (Owner == null || isPopulating || typographyEditors.Count != 6) return;
            previewTimer.Stop();
            previewTimer.Start();
        }

        private void ApplyPreview()
        {
            MainWindow owner = Owner as MainWindow;
            if (owner == null || isPopulating) return;
            AppSettings preview = owner.CreateAppearancePreview();
            preview.PanelOpacity = OpacityDial.Value / 100.0;
            preview.ClockPanelOpacity = ClockOpacityDial.Value / 100.0;
            preview.DDayPanelOpacity = DDayOpacityDial.Value / 100.0;
            preview.TextOpacity = TextOpacitySlider.Value / 100.0;
            preview.GlassStrength = GlassStrengthSlider.Value / 100.0;
            preview.GlassLightColor = selectedGlassColor;
            preview.GlassLightDirection = LightDirectionDial.Value;
            preview.PanelDepth = (int)PanelDepthSlider.Value;
            preview.ClockPanelDepth = (int)ClockDepthSlider.Value;
            preview.DDayPanelDepth = (int)DDayDepthSlider.Value;
            preview.TextColorMode = SelectedTag(TextColorModeCombo, "custom");
            try { CollectTypography(preview); }
            catch (InvalidOperationException) { return; } // Keep last valid preview during numeric editing.
            owner.PreviewAppearance(preview);
            TypographyPreviewText.Text = "현재 창 배율 " + (owner.DisplayScale * 100).ToString("0", CultureInfo.InvariantCulture) +
                "% · 달력 기준 크기 " + preview.SizeCalendar.ToString(CultureInfo.InvariantCulture) + "pt. 화면 배율은 내부에서 연속값으로 계산합니다.";
        }

        private void GlassPreset_OnClick(object sender, RoutedEventArgs e)
        {
            selectedGlassColor = Convert.ToString(((Button)sender).Tag, CultureInfo.InvariantCulture);
            UpdateGlassColorButton();
        }

        private void CustomGlassColor_OnClick(object sender, RoutedEventArgs e)
        {
            string chosen = ChooseColor(selectedGlassColor);
            if (chosen == null) return;
            selectedGlassColor = chosen;
            UpdateGlassColorButton();
        }

        private void UpdateGlassColorButton()
        {
            if (CustomGlassColorButton == null) return;
            CustomGlassColorButton.Tag = selectedGlassColor;
            CustomGlassColorButton.Content = "직접 선택 " + selectedGlassColor;
            CustomGlassColorButton.Background = new SolidColorBrush(LiquidGlassTheme.ParseColor(selectedGlassColor, Colors.LightBlue));
            PreviewSurface();
        }

        private void TypographyColor_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string chosen = ChooseColor(Convert.ToString(button.Tag, CultureInfo.InvariantCulture));
            if (chosen != null)
            {
                SetColorButton(button, chosen);
                MarkCustomTheme();
                PreviewSurface();
            }
        }

        private void TypographySelection_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            PreviewSurface();
        }

        private void TypographyText_OnChanged(object sender, TextChangedEventArgs e)
        {
            PreviewSurface();
        }

        private string ChooseColor(string current)
        {
            Color wpf = LiquidGlassTheme.ParseColor(current, Colors.White);
            using (Forms.ColorDialog dialog = new Forms.ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = DrawingColor.FromArgb(wpf.R, wpf.G, wpf.B);
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return null;
                return "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
            }
        }

        private static void SetColorButton(Button button, string color)
        {
            Color value = LiquidGlassTheme.ParseColor(color, Colors.White);
            button.Tag = color;
            button.Content = "●";
            button.ToolTip = "색상 변경 · " + color;
            button.Background = new SolidColorBrush(value);
            double luminance = 0.299 * value.R + 0.587 * value.G + 0.114 * value.B;
            button.Foreground = luminance > 160 ? Brushes.Black : Brushes.White;
        }

        private void QuickTheme_OnClick(object sender, RoutedEventArgs e)
        {
            string theme = Convert.ToString(((Button)sender).Tag, CultureInfo.InvariantCulture);
            ApplyPalette(ThemePalette.Preset(theme));
        }

        private void AutoTheme_OnClick(object sender, RoutedEventArgs e)
        {
            string chosen = ChooseColor(themeSeedColor ?? selectedGlassColor);
            if (chosen == null) return;
            ApplyPalette(ThemePalette.Generate(chosen));
        }

        private void FontPreset_OnClick(object sender, RoutedEventArgs e)
        {
            TypographyPreset preset = TypographyPreset.Find(SelectedTag(FontPresetCombo, "ko-modern"));
            bool usedFallback = false;
            isPopulating = true;
            try
            {
                foreach (TypographyEditor editor in typographyEditors)
                {
                    string requested = preset.Family(editor.Key);
                    string installed = fontNames.FirstOrDefault(name => string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));
                    if (installed == null)
                    {
                        usedFallback = true;
                        installed = fontNames.FirstOrDefault(name => string.Equals(name, TypographySize.DefaultFamily(editor.Key), StringComparison.OrdinalIgnoreCase))
                            ?? "Segoe UI";
                    }
                    editor.Font.SelectedItem = installed;
                    if (editor.Font.SelectedIndex < 0) editor.Font.Text = installed;
                    SelectByTag(editor.Weight, preset.Weight(editor.Key));
                }
            }
            finally { isPopulating = false; }
            PreviewSurface();
            AppServices.Log.Info("settings.font-preset", "preset=" + preset.Id + ";fallback=" + usedFallback);
            if (usedFallback) MessageBox.Show(this, "설치되지 않은 서체는 기본 서체로 적용했습니다. 크기와 색상은 유지됩니다.",
                "서체 적용", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyPalette(ThemePalette palette)
        {
            isPopulating = true;
            try
            {
                for (int i = 0; i < typographyEditors.Count; i++)
                {
                    TypographyEditor editor = typographyEditors[i];
                    SetColorButton(editor.Color, palette.Colors[i]);
                }
                selectedThemeId = palette.Id;
                themeSeedColor = palette.Seed;
            }
            finally { isPopulating = false; }
            UpdateThemeSelection();
            PreviewSurface();
            AppServices.Log.Info("settings.quick-theme", "theme=" + palette.Id);
        }

        private void TextColorMode_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isPopulating || typographyEditors.Count != 6) return;
            UpdateThemeSelection();
            PreviewSurface();
        }

        private void MarkCustomTheme()
        {
            if (isPopulating || typographyEditors.Count == 0) return;
            selectedThemeId = "custom";
            UpdateThemeSelection();
        }

        private void UpdateThemeSelection()
        {
            Button[] buttons =
            {
                IceThemeButton, LavenderThemeButton, MintThemeButton,
                SunsetThemeButton, YellowThemeButton, MonoThemeButton
            };
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                string id = Convert.ToString(button.Tag, CultureInfo.InvariantCulture);
                bool selected = string.Equals(id, selectedThemeId, StringComparison.OrdinalIgnoreCase);
                string name = ThemeDisplayName(id);
                ThemePalette palette = ThemePalette.Preset(id);
                string[] swatches = { palette.Colors[0], palette.Colors[3], palette.Colors[5] };
                button.Content = CreateThemeButtonContent(name, swatches, selected);
                AutomationProperties.SetName(button, name + (selected ? " 선택됨" : ""));
                button.ToolTip = name + " · " + string.Join(" / ", swatches);
                button.BorderThickness = new Thickness(1); // Selection must not change layout size.
                if (selected)
                {
                    button.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
                    button.Background = new SolidColorBrush(Color.FromArgb(178, 225, 244, 252));
                }
                else
                {
                    button.ClearValue(Button.BorderBrushProperty);
                    button.ClearValue(Button.BackgroundProperty);
                }
            }
            if (AutoThemeButton != null)
            {
                bool automatic = selectedThemeId == "auto";
                AutoThemeButton.Content = (automatic ? "✓ " : "") + "대표 색상 선택 → 조화 색상 만들기";
                AutomationProperties.SetName(AutoThemeButton, "대표 색상으로 조화 색상 만들기" + (automatic ? " 선택됨" : ""));
            }
        }

        private static UIElement CreateThemeButtonContent(string name, string[] swatches, bool selected)
        {
            StackPanel card = new StackPanel();
            StackPanel title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            title.Children.Add(new TextBlock { Text = selected ? "✓" : "", Width = 16, FontWeight = FontWeights.SemiBold });
            title.Children.Add(new TextBlock { Text = name, FontSize = 12 });
            card.Children.Add(title);
            StackPanel colors = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0) };
            foreach (string color in swatches)
                colors.Children.Add(new Border { Width = 20, Height = 10, CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(2, 0, 2, 0), Background = new SolidColorBrush(LiquidGlassTheme.ParseColor(color, Colors.White)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 80, 90, 100)), BorderThickness = new Thickness(0.7) });
            card.Children.Add(colors);
            return card;
        }

        private static string ThemeDisplayName(string id)
        {
            switch (id)
            {
                case "lavender": return "라벤더";
                case "mint": return "민트";
                case "sunset": return "선셋";
                case "yellow": return "버터 옐로";
                case "mono": return "모노";
                default: return "아이스";
            }
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed) return;
            e.Handled = true;
            try
            {
                DragMove();
                AppServices.Log.Info("settings.window.move", "completed=true");
            }
            catch (InvalidOperationException) { }
        }

        private static void SelectByTag(ComboBox comboBox, string tag)
        {
            foreach (object value in comboBox.Items)
            {
                ComboBoxItem item = value as ComboBoxItem;
                if (item != null && string.Equals(Convert.ToString(item.Tag), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
        }

        private static string SelectedTag(ComboBox comboBox, string fallback)
        {
            ComboBoxItem item = comboBox.SelectedItem as ComboBoxItem;
            return item == null ? fallback : Convert.ToString(item.Tag, CultureInfo.InvariantCulture);
        }
    }
}
