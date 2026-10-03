using System;
using System.Collections.Generic;

namespace DDay3.Models
{
    internal sealed class AppSettings
    {
        internal int X { get; set; }
        internal int Y { get; set; }
        internal int Width { get; set; }
        internal int Height { get; set; }
        internal double PanelOpacity { get; set; }
        internal double ClockPanelOpacity { get; set; }
        internal double DDayPanelOpacity { get; set; }
        internal bool ShowPanelOutline { get; set; }
        internal bool ShowHoverReflection { get; set; }
        internal double TextOpacity { get; set; }
        internal bool Topmost { get; set; }
        internal bool AutoStart { get; set; }
        internal bool ShowCalendar { get; set; }
        internal bool ShowKoreanHolidays { get; set; }
        internal bool ShowSolarTerms { get; set; }
        internal int VisibleDDayCount { get; set; }
        internal string TimeFormat { get; set; }
        internal bool ShowSeconds { get; set; }
        internal bool ShowLunarDate { get; set; }
        internal string ClockMode { get; set; }
        internal bool SnapToGrid { get; set; }
        internal int GridSize { get; set; }
        internal string DateFormat { get; set; }
        internal string DayFormat { get; set; }
        internal double GlassStrength { get; set; }
        internal string GlassLightColor { get; set; }
        internal double GlassLightDirection { get; set; }
        internal int PanelDepth { get; set; }
        internal int ClockPanelDepth { get; set; }
        internal int DDayPanelDepth { get; set; }
        internal string ThemeId { get; set; }
        internal string TextColorMode { get; set; }
        internal bool CalendarWeekendColors { get; set; }
        internal string WeekStart { get; set; }
        internal string ThemeSeedColor { get; set; }

        internal string ColorTime { get; set; }
        internal string ColorDate { get; set; }
        internal string ColorDDayTitle { get; set; }
        internal string ColorDDayCount { get; set; }
        internal string ColorDDayDate { get; set; }
        internal string ColorCalendar { get; set; }

        internal string FontTime { get; set; }
        internal string FontDate { get; set; }
        internal string FontDDayTitle { get; set; }
        internal string FontDDayCount { get; set; }
        internal string FontDDayDate { get; set; }
        internal string FontCalendar { get; set; }

        internal string WeightTime { get; set; }
        internal string WeightDate { get; set; }
        internal string WeightDDayTitle { get; set; }
        internal string WeightDDayCount { get; set; }
        internal string WeightDDayDate { get; set; }
        internal string WeightCalendar { get; set; }

        internal int SizeTime { get; set; }
        internal int SizeDate { get; set; }
        internal int SizeDDayTitle { get; set; }
        internal int SizeDDayCount { get; set; }
        internal int SizeDDayDate { get; set; }
        internal int SizeCalendar { get; set; }
        internal List<DDayItem> Items { get; private set; }

        internal AppSettings()
        {
            Items = new List<DDayItem>();
        }

        internal static AppSettings CreateDefault()
        {
            AppSettings value = new AppSettings
            {
                X = 100,
                Y = 100,
                Width = 350,
                Height = 250,
                PanelOpacity = 0.70,
                ClockPanelOpacity = 0.60,
                DDayPanelOpacity = 0.60,
                ShowPanelOutline = true,
                ShowHoverReflection = true,
                TextOpacity = 1.0,
                Topmost = false,
                AutoStart = false,
                ShowCalendar = false,
                VisibleDDayCount = 3,
                TimeFormat = "24h",
                ShowSeconds = true,
                ShowLunarDate = false,
                ClockMode = "clock",
                SnapToGrid = false,
                GridSize = 16,
                DateFormat = "yyyy-mm-dd",
                DayFormat = "kor",
                GlassStrength = 0.18,
                GlassLightColor = "#DCDCDC",
                GlassLightDirection = 315,
                PanelDepth = 35,
                ClockPanelDepth = 20,
                DDayPanelDepth = 20,
                ThemeId = "mono",
                TextColorMode = "light",
                WeekStart = "Sunday",
                CalendarWeekendColors = true,
                ThemeSeedColor = "#D5DEE6",
                ColorTime = "#FFFFFF",
                ColorDate = "#E4E8EC",
                ColorDDayTitle = "#FFFFFF",
                ColorDDayCount = "#C8D0D8",
                ColorDDayDate = "#BBC4CC",
                ColorCalendar = "#F1F3F5",
                FontTime = "Consolas",
                FontDate = "Malgun Gothic",
                FontDDayTitle = "Malgun Gothic",
                FontDDayCount = "Tahoma",
                FontDDayDate = "Malgun Gothic",
                FontCalendar = "Tahoma",
                WeightTime = "Normal",
                WeightDate = "Normal",
                WeightDDayTitle = "Normal",
                WeightDDayCount = "Normal",
                WeightDDayDate = "Normal",
                WeightCalendar = "Normal",
                SizeTime = 45,
                SizeDate = 12,
                SizeDDayTitle = 12,
                SizeDDayCount = 15,
                SizeDDayDate = 10,
                SizeCalendar = 15
            };
            value.Items.Add(new DDayItem { Title = "D-Day", Date = DateTime.Today });
            return value;
        }

        // Reset presentation without mutating the editor's schedule objects or saved geometry.
        internal AppSettings ResetPresentation()
        {
            AppSettings value = CreateDefault();
            value.X = X; value.Y = Y; value.Width = Width; value.Height = Height;
            value.Items.Clear();
            foreach (DDayItem item in Items) value.Items.Add(item.Clone());
            return value;
        }

        internal AppSettings Clone()
        {
            AppSettings copy = (AppSettings)MemberwiseClone();
            copy.Items = new List<DDayItem>();
            foreach (DDayItem item in Items) copy.Items.Add(item.Clone());
            return copy;
        }
    }
}
