using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DDay3.Diagnostics;
using DDay3.Models;

namespace DDay3.Services
{
    internal sealed class ConfigLoadResult
    {
        internal AppSettings Settings { get; set; }
        internal string Status { get; set; }
        internal string Source { get; set; }
        internal string Message { get; set; }
        internal bool SafeToAutoSave { get; set; }
    }

    internal sealed class ConfigSaveResult
    {
        internal bool Success { get; set; }
        internal bool Skipped { get; set; }
        internal string Message { get; set; }
        internal string Path { get; set; }
    }

    internal sealed class ConfigurationService
    {
        private const string ConfigFileName = "dday_config.ini";
        private const int BackupCount = 3;
        private readonly IDiagnosticLog log;
        private bool safeToAutoSave = true;

        internal string ConfigDirectory { get; private set; }
        internal string ConfigPath { get; private set; }

        internal ConfigurationService(IDiagnosticLog log, string configDirectory = null)
        {
            this.log = log;
            ConfigDirectory = configDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyDDayWidget");
            Directory.CreateDirectory(ConfigDirectory);
            ConfigPath = Path.Combine(ConfigDirectory, ConfigFileName);
            if (configDirectory == null) MigrateLegacyFileIfNeeded();
        }

        internal ConfigLoadResult Load()
        {
            ConfigLoadResult primary = ReadFile(ConfigPath);
            if (primary.Status == "ok" || primary.Status == "partial")
            {
                safeToAutoSave = true;
                primary.SafeToAutoSave = true;
                return primary;
            }

            if (primary.Status == "missing")
            {
                safeToAutoSave = true;
                return new ConfigLoadResult
                {
                    Settings = AppSettings.CreateDefault(),
                    Status = "defaults_new",
                    Message = "새 기본 설정으로 시작합니다.",
                    SafeToAutoSave = true
                };
            }

            for (int i = 1; i <= BackupCount; i++)
            {
                string backup = BackupPath(i);
                ConfigLoadResult recovered = ReadFile(backup);
                if (recovered.Status != "ok" && recovered.Status != "partial") continue;
                PreserveCorruptFile(ConfigPath);
                try
                {
                    AtomicWrite(ConfigPath, Serialize(recovered.Settings));
                    recovered.Status = "backup_recovered";
                    recovered.Message = Path.GetFileName(backup) + "에서 설정을 복구했습니다.";
                    recovered.SafeToAutoSave = true;
                    safeToAutoSave = true;
                }
                catch (Exception ex)
                {
                    recovered.Status = "backup_recovered_readonly";
                    recovered.Message = "백업을 읽었지만 기본 설정 파일을 복구하지 못했습니다.";
                    recovered.SafeToAutoSave = false;
                    safeToAutoSave = false;
                    log.Error("config.recovery.write", ex);
                }
                return recovered;
            }

            safeToAutoSave = false;
            PreserveCorruptFile(ConfigPath);
            return new ConfigLoadResult
            {
                Settings = AppSettings.CreateDefault(),
                Status = "defaults_after_error",
                Message = "설정 파일과 백업을 읽을 수 없어 기본값으로 실행합니다. 기존 파일은 덮어쓰지 않습니다.",
                SafeToAutoSave = false
            };
        }

        internal ConfigSaveResult Save(AppSettings settings, bool userInitiated)
        {
            if (!safeToAutoSave && !userInitiated)
            {
                return new ConfigSaveResult
                {
                    Success = false,
                    Skipped = true,
                    Message = "손상된 설정 보호를 위해 자동 저장을 건너뛰었습니다."
                };
            }

            try
            {
                Normalize(settings);
                RotateBackups();
                AtomicWrite(ConfigPath, Serialize(settings));
                safeToAutoSave = true;
                log.Info("config.save", "items=" + settings.Items.Count + ", userInitiated=" + userInitiated);
                return new ConfigSaveResult
                {
                    Success = true,
                    Message = "설정을 저장했습니다.",
                    Path = ConfigPath
                };
            }
            catch (Exception ex)
            {
                log.Error("config.save.failed", ex);
                return new ConfigSaveResult { Success = false, Message = ex.Message };
            }
        }

        internal ConfigLoadResult Import(string path)
        {
            ConfigLoadResult result = ReadFile(path);
            if (result.Status == "ok" || result.Status == "partial")
            {
                result.Status = "imported";
                result.SafeToAutoSave = true;
            }
            return result;
        }

        internal ConfigLoadResult RestoreLatestBackup()
        {
            for (int i = 1; i <= BackupCount; i++)
            {
                ConfigLoadResult result = ReadFile(BackupPath(i));
                if (result.Status == "ok" || result.Status == "partial")
                {
                    result.Status = "backup_loaded";
                    result.SafeToAutoSave = true;
                    return result;
                }
            }
            return new ConfigLoadResult
            {
                Settings = AppSettings.CreateDefault(),
                Status = "backup_missing",
                Message = "사용 가능한 설정 백업이 없습니다.",
                SafeToAutoSave = false
            };
        }

        internal void Export(AppSettings settings, string destination)
        {
            Normalize(settings);
            AtomicWrite(destination, Serialize(settings));
            log.Info("config.export", "completed");
        }

        private ConfigLoadResult ReadFile(string path)
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult { Settings = AppSettings.CreateDefault(), Status = "missing", Source = path };
            }

            try
            {
                Dictionary<string, Dictionary<string, string>> ini = ParseIni(File.ReadAllLines(path, Encoding.UTF8));
                Dictionary<string, string> window;
                if (!ini.TryGetValue("Window", out window)) throw new InvalidDataException("[Window] 구역이 없습니다.");

                AppSettings settings = AppSettings.CreateDefault();
                List<string> warnings = new List<string>();
                settings.X = ReadInt(window, "x", settings.X, -100000, 100000, warnings);
                settings.Y = ReadInt(window, "y", settings.Y, -100000, 100000, warnings);
                settings.Width = ReadInt(window, "w", settings.Width, 180, 10000, warnings);
                settings.Height = ReadInt(window, "h", settings.Height, 64, 10000, warnings);
                double legacyAlpha = ReadDouble(window, "alpha", settings.PanelOpacity, 0.05, 1.00, warnings);
                settings.PanelOpacity = ReadDouble(window, "panel_opacity", legacyAlpha, 0.05, 1.00, warnings);
                settings.ClockPanelOpacity = ReadDouble(window, "clock_panel_opacity", settings.PanelOpacity, 0.05, 1.00, warnings);
                settings.DDayPanelOpacity = ReadDouble(window, "dday_panel_opacity", settings.PanelOpacity, 0.05, 1.00, warnings);
                settings.ShowPanelOutline = ReadBool(window, "show_panel_outline", settings.ShowPanelOutline, warnings);
                settings.ShowHoverReflection = ReadBool(window, "show_hover_reflection", settings.ShowHoverReflection, warnings);
                settings.TextOpacity = ReadDouble(window, "text_opacity", 1.0, 0.40, 1.00, warnings);
                settings.Topmost = ReadBool(window, "topmost", settings.Topmost, warnings);
                // 3.0.0-dev.2부터 유리판은 항상 사용합니다. 이전 INI의 false 값은 무시합니다.
                settings.ShowCalendar = ReadBool(window, "show_calendar", settings.ShowCalendar, warnings);
                settings.ShowKoreanHolidays = ReadBool(window, "show_korean_holidays", false, warnings);
                settings.ShowSolarTerms = ReadBool(window, "show_solar_terms", false, warnings);
                settings.ShowSeconds = ReadBool(window, "show_seconds", settings.ShowSeconds, warnings);
                settings.ShowLunarDate = ReadBool(window, "show_lunar_date", settings.ShowLunarDate, warnings);
                settings.ClockMode = ReadEnum(window, "clock_mode", "clock", new[] { "clock", "stopwatch" }, warnings);
                settings.SnapToGrid = ReadBool(window, "snap_to_grid", false, warnings);
                settings.GridSize = ReadInt(window, "grid_size", 16, 8, 64, warnings);
                settings.VisibleDDayCount = ReadInt(window, "visible_dday_count", 3, 1, 10, warnings);
                settings.AutoStart = ReadBool(window, "auto_start", settings.AutoStart, warnings);
                settings.TimeFormat = ReadEnum(window, "time_format", settings.TimeFormat, new[] { "12h", "24h" }, warnings);
                settings.DateFormat = ReadEnum(window, "date_format", settings.DateFormat,
                    new[] { "yyyy-mm-dd", "mm/dd/yyyy", "dd/mm/yyyy" }, warnings);
                settings.DayFormat = ReadEnum(window, "day_format", settings.DayFormat, new[] { "kor", "eng" }, warnings);
                settings.GlassStrength = ReadDouble(window, "glass_strength", settings.GlassStrength, 0.10, 1.0, warnings);
                settings.GlassLightColor = ReadColor(window, "glass_light_color", settings.GlassLightColor, warnings);
                settings.GlassLightDirection = ReadDouble(window, "glass_light_direction", settings.GlassLightDirection, 0, 360, warnings);
                settings.PanelDepth = ReadInt(window, "panel_depth", settings.PanelDepth, -100, 200, warnings);
                settings.ClockPanelDepth = ReadInt(window, "clock_panel_depth", settings.ClockPanelDepth, -100, 100, warnings);
                settings.DDayPanelDepth = ReadInt(window, "dday_panel_depth", settings.DDayPanelDepth, -100, 100, warnings);
                settings.ThemeId = ReadEnum(window, "theme_id", "custom",
                    new[] { "ice", "lavender", "mint", "sunset", "yellow", "mono", "auto", "custom" }, warnings);
                settings.TextColorMode = ReadEnum(window, "text_color_mode", "custom", new[] { "light", "dark", "custom" }, warnings);
                settings.WeekStart = ReadEnum(window, "week_start", "Sunday", new[] { "Sunday", "Monday" }, warnings);
                settings.ThemeSeedColor = ReadColor(window, "theme_seed_color", settings.GlassLightColor, warnings);

                settings.ColorTime = ReadColor(window, "color_time", settings.ColorTime, warnings);
                settings.ColorDate = ReadColor(window, "color_date", settings.ColorDate, warnings);
                settings.ColorDDayTitle = ReadColor(window, "color_dday_title", settings.ColorDDayTitle, warnings);
                settings.ColorDDayCount = ReadColor(window, "color_dday_count", settings.ColorDDayCount, warnings);
                settings.ColorDDayDate = ReadColor(window, "color_dday_date", settings.ColorDDayDate, warnings);
                settings.ColorCalendar = ReadColor(window, "color_calendar", settings.ColorCalendar, warnings);

                settings.FontTime = ReadText(window, "font_time", settings.FontTime);
                settings.FontDate = ReadText(window, "font_date", settings.FontDate);
                settings.FontDDayTitle = ReadText(window, "font_dday_title", settings.FontDDayTitle);
                settings.FontDDayCount = ReadText(window, "font_dday_count", settings.FontDDayCount);
                settings.FontDDayDate = ReadText(window, "font_dday_date", settings.FontDDayDate);
                settings.FontCalendar = ReadText(window, "font_calendar", settings.FontCalendar);

                string[] weights = { "Normal", "Medium", "SemiBold", "Bold" };
                settings.WeightTime = ReadEnum(window, "weight_time", settings.WeightTime, weights, warnings);
                settings.WeightDate = ReadEnum(window, "weight_date", settings.WeightDate, weights, warnings);
                settings.WeightDDayTitle = ReadEnum(window, "weight_dday_title", settings.WeightDDayTitle, weights, warnings);
                settings.WeightDDayCount = ReadEnum(window, "weight_dday_count", settings.WeightDDayCount, weights, warnings);
                settings.WeightDDayDate = ReadEnum(window, "weight_dday_date", settings.WeightDDayDate, weights, warnings);
                settings.WeightCalendar = ReadEnum(window, "weight_calendar", settings.WeightCalendar, weights, warnings);

                settings.CalendarWeekendColors = ReadBool(window, "calendar_weekend_colors", true, warnings);
                settings.SizeTime = ReadFontSize(window, "size_time", settings.SizeTime, warnings);
                settings.SizeDate = ReadFontSize(window, "size_date", settings.SizeDate, warnings);
                settings.SizeDDayTitle = ReadFontSize(window, "size_dday_title", settings.SizeDDayTitle, warnings);
                settings.SizeDDayCount = ReadFontSize(window, "size_dday_count", settings.SizeDDayCount, warnings);
                settings.SizeDDayDate = ReadFontSize(window, "size_dday_date", settings.SizeDDayDate, warnings);
                settings.SizeCalendar = ReadFontSize(window, "size_calendar", settings.SizeCalendar, warnings);

                settings.Items.Clear();
                foreach (KeyValuePair<string, Dictionary<string, string>> section in ini
                    .Where(value => value.Key.StartsWith("DDay-", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(value => SectionNumber(value.Key)))
                {
                    string title;
                    string dateText;
                    DateTime date;
                    if (!section.Value.TryGetValue("title", out title)) title = "D-Day";
                    if (!section.Value.TryGetValue("date", out dateText) ||
                        !DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out date))
                    {
                        warnings.Add(section.Key + " 날짜가 잘못되어 제외했습니다.");
                        continue;
                    }
                    string googleKey;
                    if (!section.Value.TryGetValue("calendar_event_key", out googleKey))
                        section.Value.TryGetValue("google_event_key", out googleKey);
                    settings.Items.Add(new DDayItem { Title = CleanTitle(title), Date = date.Date,
                        CalendarEventKey = CalendarEvent.NormalizeKey(googleKey) });
                }
                if (settings.Items.Count == 0)
                {
                    settings.Items.Add(new DDayItem { Title = "D-Day", Date = DateTime.Today });
                    warnings.Add("유효한 D-Day가 없어 기본 항목을 추가했습니다.");
                }
                Normalize(settings);
                return new ConfigLoadResult
                {
                    Settings = settings,
                    Status = warnings.Count == 0 ? "ok" : "partial",
                    Source = path,
                    Message = string.Join(" ", warnings),
                    SafeToAutoSave = true
                };
            }
            catch (Exception ex)
            {
                log.Error("config.read.failed", ex, "file=" + Path.GetFileName(path));
                return new ConfigLoadResult
                {
                    Settings = AppSettings.CreateDefault(),
                    Status = "error",
                    Source = path,
                    Message = ex.Message,
                    SafeToAutoSave = false
                };
            }
        }

        private void MigrateLegacyFileIfNeeded()
        {
            if (File.Exists(ConfigPath)) return;
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(Environment.CurrentDirectory, ConfigFileName),
                Path.Combine(executableDirectory, ConfigFileName)
            };
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(candidate) || string.Equals(candidate, ConfigPath, StringComparison.OrdinalIgnoreCase)) continue;
                ConfigLoadResult result = ReadFile(candidate);
                if (result.Status != "ok" && result.Status != "partial") continue;
                File.Copy(candidate, ConfigPath, false);
                log.Info("config.migrate", "legacy configuration copied");
                break;
            }
        }

        private void RotateBackups()
        {
            if (!File.Exists(ConfigPath)) return;
            ConfigLoadResult current = ReadFile(ConfigPath);
            if (current.Status != "ok" && current.Status != "partial") return;
            for (int i = BackupCount; i >= 2; i--)
            {
                string source = BackupPath(i - 1);
                if (File.Exists(source)) File.Copy(source, BackupPath(i), true);
            }
            File.Copy(ConfigPath, BackupPath(1), true);
        }

        private string BackupPath(int index)
        {
            return Path.Combine(ConfigDirectory, "dday_config.bak" + index + ".ini");
        }

        private void PreserveCorruptFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                string destination = Path.Combine(ConfigDirectory,
                    "dday_config.corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".ini");
                File.Copy(path, destination, false);
            }
            catch (Exception ex)
            {
                log.Error("config.preserve.failed", ex);
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ParseIni(string[] lines)
        {
            Dictionary<string, Dictionary<string, string>> result =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> current = null;
            foreach (string sourceLine in lines)
            {
                string line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[name] = current;
                    continue;
                }
                if (current == null) continue;
                int split = line.IndexOf('=');
                if (split < 0) split = line.IndexOf(':');
                if (split <= 0) continue;
                current[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
            return result;
        }

        private static string Serialize(AppSettings value)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("[Meta]");
            text.AppendLine("schema_version = 15");
            text.AppendLine("saved_at = " + DateTimeOffset.Now.ToString("o"));
            text.AppendLine();
            text.AppendLine("[Window]");
            Write(text, "x", value.X);
            Write(text, "y", value.Y);
            Write(text, "w", value.Width);
            Write(text, "h", value.Height);
            Write(text, "alpha", "1.00"); // Legacy applications also keep text readable after downgrade.
            Write(text, "panel_opacity", value.PanelOpacity.ToString("0.00", CultureInfo.InvariantCulture));
            Write(text, "clock_panel_opacity", value.ClockPanelOpacity.ToString("0.00", CultureInfo.InvariantCulture));
            Write(text, "dday_panel_opacity", value.DDayPanelOpacity.ToString("0.00", CultureInfo.InvariantCulture));
            Write(text, "show_panel_outline", value.ShowPanelOutline);
            Write(text, "show_hover_reflection", value.ShowHoverReflection);
            Write(text, "text_opacity", value.TextOpacity.ToString("0.00", CultureInfo.InvariantCulture));
            Write(text, "topmost", value.Topmost);
            Write(text, "use_glass_background", true);
            Write(text, "show_calendar", value.ShowCalendar);
            Write(text, "show_korean_holidays", value.ShowKoreanHolidays);
            Write(text, "show_solar_terms", value.ShowSolarTerms);
            Write(text, "visible_dday_count", value.VisibleDDayCount);
            Write(text, "auto_start", value.AutoStart);
            Write(text, "show_seconds", value.ShowSeconds);
            Write(text, "show_lunar_date", value.ShowLunarDate);
            Write(text, "clock_mode", value.ClockMode);
            Write(text, "snap_to_grid", value.SnapToGrid);
            Write(text, "grid_size", value.GridSize);
            Write(text, "time_format", value.TimeFormat);
            Write(text, "date_format", value.DateFormat);
            Write(text, "day_format", value.DayFormat);
            Write(text, "glass_strength", value.GlassStrength.ToString("0.00", CultureInfo.InvariantCulture));
            Write(text, "glass_light_color", value.GlassLightColor);
            Write(text, "glass_light_direction", value.GlassLightDirection.ToString("0", CultureInfo.InvariantCulture));
            Write(text, "panel_depth", value.PanelDepth);
            Write(text, "clock_panel_depth", value.ClockPanelDepth);
            Write(text, "dday_panel_depth", value.DDayPanelDepth);
            Write(text, "theme_id", value.ThemeId);
            Write(text, "text_color_mode", value.TextColorMode);
            Write(text, "week_start", value.WeekStart);
            Write(text, "theme_seed_color", value.ThemeSeedColor);
            Write(text, "color_time", value.ColorTime);
            Write(text, "color_date", value.ColorDate);
            Write(text, "color_dday_title", value.ColorDDayTitle);
            Write(text, "color_dday_count", value.ColorDDayCount);
            Write(text, "color_dday_date", value.ColorDDayDate);
            Write(text, "color_calendar", value.ColorCalendar);
            Write(text, "font_time", value.FontTime);
            Write(text, "font_date", value.FontDate);
            Write(text, "font_dday_title", value.FontDDayTitle);
            Write(text, "font_dday_count", value.FontDDayCount);
            Write(text, "font_dday_date", value.FontDDayDate);
            Write(text, "font_calendar", value.FontCalendar);
            Write(text, "weight_time", value.WeightTime);
            Write(text, "weight_date", value.WeightDate);
            Write(text, "weight_dday_title", value.WeightDDayTitle);
            Write(text, "weight_dday_count", value.WeightDDayCount);
            Write(text, "weight_dday_date", value.WeightDDayDate);
            Write(text, "weight_calendar", value.WeightCalendar);
            Write(text, "calendar_weekend_colors", value.CalendarWeekendColors);
            Write(text, "size_time", value.SizeTime);
            Write(text, "size_date", value.SizeDate);
            Write(text, "size_dday_title", value.SizeDDayTitle);
            Write(text, "size_dday_count", value.SizeDDayCount);
            Write(text, "size_dday_date", value.SizeDDayDate);
            Write(text, "size_calendar", value.SizeCalendar);
            for (int i = 0; i < value.Items.Count; i++)
            {
                text.AppendLine();
                text.AppendLine("[DDay-" + (i + 1) + "]");
                Write(text, "title", CleanTitle(value.Items[i].Title));
                Write(text, "date", value.Items[i].Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                string googleKey = CalendarEvent.NormalizeKey(value.Items[i].CalendarEventKey);
                if (googleKey.Length > 0) Write(text, "calendar_event_key", googleKey);
            }
            return text.ToString();
        }

        private static void Write(StringBuilder text, string key, object value)
        {
            text.Append(key).Append(" = ").AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void AtomicWrite(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            catch
            {
                File.Copy(temporary, path, true);
                File.Delete(temporary);
            }
        }

        private static void Normalize(AppSettings value)
        {
            value.SizeTime = NormalizeSize(value.SizeTime, "time");
            value.SizeDate = NormalizeSize(value.SizeDate, "date");
            value.SizeDDayTitle = NormalizeSize(value.SizeDDayTitle, "dday_title");
            value.SizeDDayCount = NormalizeSize(value.SizeDDayCount, "dday_count");
            value.SizeDDayDate = NormalizeSize(value.SizeDDayDate, "dday_date");
            value.SizeCalendar = NormalizeSize(value.SizeCalendar, "calendar");
            value.Width = Clamp(value.Width, 180, 10000);
            value.Height = Clamp(value.Height, 64, 10000);
            value.PanelOpacity = Clamp(value.PanelOpacity, 0.05, 1.0);
            value.ClockPanelOpacity = Clamp(value.ClockPanelOpacity, 0.05, 1.0);
            value.DDayPanelOpacity = Clamp(value.DDayPanelOpacity, 0.05, 1.0);
            value.TextOpacity = Clamp(value.TextOpacity, 0.40, 1.0);
            value.VisibleDDayCount = Clamp(value.VisibleDDayCount, 1, 10);
            value.GridSize = Clamp(value.GridSize, 8, 64);
            value.GlassStrength = Clamp(value.GlassStrength, 0.10, 1.0);
            value.GlassLightDirection = GlassLighting.NormalizeDirection(value.GlassLightDirection);
            value.PanelDepth = GlassLighting.NormalizeDepth(value.PanelDepth, true);
            value.ClockPanelDepth = GlassLighting.NormalizeDepth(value.ClockPanelDepth);
            value.DDayPanelDepth = GlassLighting.NormalizeDepth(value.DDayPanelDepth);
            if (string.IsNullOrWhiteSpace(value.ThemeId)) value.ThemeId = "custom";
            if (value.Items.Count == 0) value.Items.Add(new DDayItem { Title = "D-Day", Date = DateTime.Today });
            foreach (DDayItem item in value.Items)
            {
                item.Title = CleanTitle(item.Title);
                item.Date = item.Date.Date;
                item.CalendarEventKey = CalendarEvent.NormalizeKey(item.CalendarEventKey);
            }
        }

        private static int NormalizeSize(double value, string role)
        {
            int points;
            return TypographySize.TryNormalize(value, out points) ? points : TypographySize.DefaultPoints(role);
        }

        private static int ReadFontSize(Dictionary<string, string> section, string key, int fallback,
            List<string> warnings)
        {
            string text;
            double value;
            int points;
            if (!section.TryGetValue(key, out text)) return fallback;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                !TypographySize.TryNormalize(value, out points))
            {
                warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
                return fallback;
            }
            return points;
        }

        private static string CleanTitle(string value)
        {
            string result = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (result.Length == 0) result = "D-Day";
            return result.Length > 120 ? result.Substring(0, 120) : result;
        }

        private static int SectionNumber(string name)
        {
            int number;
            return int.TryParse(name.Substring(name.IndexOf('-') + 1), out number) ? number : int.MaxValue;
        }

        private static int ReadInt(Dictionary<string, string> section, string key, int fallback,
            int minimum, int maximum, List<string> warnings)
        {
            string text;
            int value;
            if (!section.TryGetValue(key, out text)) return fallback;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ||
                value < minimum || value > maximum)
            {
                warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
                return fallback;
            }
            return value;
        }

        private static double ReadDouble(Dictionary<string, string> section, string key, double fallback,
            double minimum, double maximum, List<string> warnings)
        {
            string text;
            double value;
            if (!section.TryGetValue(key, out text)) return fallback;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
            {
                warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
                return fallback;
            }
            return value;
        }

        private static bool ReadBool(Dictionary<string, string> section, string key, bool fallback,
            List<string> warnings)
        {
            string text;
            bool value;
            if (!section.TryGetValue(key, out text)) return fallback;
            if (bool.TryParse(text, out value)) return value;
            if (text == "1") return true;
            if (text == "0") return false;
            warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
            return fallback;
        }

        private static string ReadEnum(Dictionary<string, string> section, string key, string fallback,
            IEnumerable<string> allowed, List<string> warnings)
        {
            string value;
            if (!section.TryGetValue(key, out value)) return fallback;
            string canonical = allowed.FirstOrDefault(candidate =>
                string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
            if (canonical != null) return canonical;
            warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
            return fallback;
        }

        private static string ReadColor(Dictionary<string, string> section, string key, string fallback,
            List<string> warnings)
        {
            string value;
            if (!section.TryGetValue(key, out value)) return fallback;
            if (value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit)) return value.ToUpperInvariant();
            warnings.Add(key + " 값이 잘못되어 기본값을 사용합니다.");
            return fallback;
        }

        private static string ReadText(Dictionary<string, string> section, string key, string fallback)
        {
            string value;
            return section.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
        }

        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        private static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
