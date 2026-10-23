using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        internal string ContentHash { get; set; }
        internal string Content { get; set; }
        internal DateTimeOffset SavedAt { get; set; }
        internal int SchemaVersion { get; set; }
        public string Description { get { return SavedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") +
            " · D-Day " + Settings.Items.Count + "개 · " + Path.GetFileName(Source); } }
    }

    internal sealed class ConfigSaveResult
    {
        internal bool Success { get; set; }
        internal bool Skipped { get; set; }
        internal string Message { get; set; }
        internal string Path { get; set; }
        internal bool RequiresRecoveryConfirmation { get; set; }
    }

    internal sealed class ConfigurationService
    {
        private const string ConfigFileName = "dday_config.ini";
        private const int BackupCount = 3;
        private const int DailyBackupCount = 14;
        private readonly IDiagnosticLog log;
        private readonly object gate = new object();
        private bool safeToAutoSave;
        private string observedHash;
        private readonly bool allowLegacyMigration;
        private bool legacyUnavailable;

        internal bool IsWriteProtected { get { return !safeToAutoSave; } }

        internal string ConfigDirectory { get; private set; }
        internal string ConfigPath { get; private set; }

        internal ConfigurationService(IDiagnosticLog log, string configDirectory = null)
        {
            this.log = log;
            allowLegacyMigration = configDirectory == null;
            ConfigDirectory = configDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyDDayWidget");
            Directory.CreateDirectory(ConfigDirectory);
            ConfigPath = Path.Combine(ConfigDirectory, ConfigFileName);
        }

        internal ConfigLoadResult Load()
        {
            lock (gate) return LoadCore();
        }

        private ConfigLoadResult LoadCore()
        {
            safeToAutoSave = false;
            ConfigLoadResult primary = ReadWithRetry(ConfigPath);
            observedHash = primary.ContentHash;
            if (primary.Status == "ok")
            {
                safeToAutoSave = true;
                primary.SafeToAutoSave = true;
                TryDailyBackup(primary);
                if (primary.SchemaVersion < 19) UpgradeLegacyIntegrity(primary);
                return primary;
            }

            List<ConfigLoadResult> backups = GetBackupCandidatesCore();
            if (backups.Count > 0)
            {
                ConfigLoadResult recovered = backups[0];
                try
                {
                    using (OpenWriteGate())
                    {
                        if (primary.Status == "unavailable") throw new IOException("설정 파일에 접근할 수 없습니다.");
                        EnsureUnchanged(primary);
                        PreserveRecoveryFiles();
                        string restoredContent = Serialize(recovered.Settings);
                        AtomicWrite(ConfigPath, restoredContent);
                        observedHash = Hash(restoredContent);
                        safeToAutoSave = true;
                        recovered.Status = "backup_recovered";
                        recovered.SafeToAutoSave = true;
                        recovered.Message = Path.GetFileName(recovered.Source) + "에서 설정을 복구했습니다. 이전 파일은 Recovery 폴더에 보관했습니다.";
                        TryDailyBackup(recovered);
                    }
                }
                catch (Exception ex)
                {
                    recovered.Status = "backup_recovered_readonly";
                    recovered.SafeToAutoSave = false;
                    recovered.Message = "백업으로 표시하지만 파일 접근 문제로 저장을 보호하고 있습니다. 잠시 후 다시 실행하거나 설정에서 확인해 주세요.";
                    log.Error("config.recovery.write", ex);
                }
                return recovered;
            }

            // Legacy files are consulted only after this user's backups. A busy
            // working directory must never win over an existing recovery copy.
            if (primary.Status == "missing" && !HasConfigurationHistory())
            {
                ConfigLoadResult legacy = ReadLegacyConfiguration();
                if (legacy != null)
                {
                    try
                    {
                        using (OpenWriteGate())
                        {
                            EnsureUnchanged(primary);
                            string migratedContent = Serialize(legacy.Settings);
                            AtomicWrite(ConfigPath, migratedContent);
                            observedHash = Hash(migratedContent);
                            safeToAutoSave = true;
                            legacy.SafeToAutoSave = true;
                            TryDailyBackup(legacy);
                        }
                    }
                    catch (Exception ex)
                    { legacy.SafeToAutoSave = false; log.Error("config.migrate.failed", ex); }
                    legacy.Status = safeToAutoSave ? "ok" : "backup_recovered_readonly";
                    legacy.Message = safeToAutoSave ? "기존 설정을 이어서 사용합니다." : "기존 설정을 읽었지만 파일 저장은 보호하고 있습니다.";
                    return legacy;
                }
                // Unavailable legacy paths are not evidence of a first launch.
                if (!legacyUnavailable)
                {
                    safeToAutoSave = true;
                    return new ConfigLoadResult { Settings = AppSettings.CreateDefault(), Status = "defaults_new",
                        Message = "새 기본 설정으로 시작합니다.", SafeToAutoSave = true };
                }
            }

            try { PreserveRecoveryFiles(); } catch (Exception ex) { log.Error("config.preserve.failed", ex); }
            return new ConfigLoadResult
            {
                Settings = primary.Status == "partial" ? primary.Settings : AppSettings.CreateDefault(),
                Status = "defaults_protected",
                Message = "설정을 온전히 읽지 못해 임시 화면으로 실행합니다. 이동·종료·메뉴 조작으로 기존 파일과 백업을 덮어쓰지 않습니다. 설정에서 백업을 선택하거나 파일을 불러와 확인해 주세요.",
                SafeToAutoSave = false
            };
        }

        internal ConfigSaveResult Save(AppSettings settings, bool userInitiated, bool confirmRecovery = false)
        {
            lock (gate) return SaveCore(settings, userInitiated, confirmRecovery);
        }

        private ConfigSaveResult SaveCore(AppSettings settings, bool userInitiated, bool confirmRecovery)
        {
            if (!safeToAutoSave && !(userInitiated && confirmRecovery))
            {
                return new ConfigSaveResult
                {
                    Success = false,
                    Skipped = !userInitiated,
                    RequiresRecoveryConfirmation = true,
                    Message = "설정 파일 보호로 저장을 중지했습니다. 설정에서 백업 또는 불러온 내용을 확인한 뒤 저장해 주세요."
                };
            }

            try
            {
                using (OpenWriteGate())
                {
                    ConfigLoadResult current = ReadFile(ConfigPath);
                    if (current.Status == "unavailable") throw new IOException("설정 파일에 접근할 수 없어 저장하지 않았습니다.");
                    bool changedOutside = current.ContentHash != observedHash ||
                        (current.Status != "ok" && current.Status != "missing") ||
                        (current.Status == "missing" && observedHash == null && HasConfigurationHistory());
                    if (changedOutside && !(userInitiated && confirmRecovery))
                    {
                        safeToAutoSave = false;
                        return new ConfigSaveResult { Success = false, Skipped = !userInitiated,
                            RequiresRecoveryConfirmation = true,
                            Message = "실행 중 설정 파일이 변경되었거나 손상되어 저장을 보호했습니다. 설정에서 내용을 확인해 주세요." };
                    }
                    if (current.Status == "missing" && observedHash != null && !confirmRecovery)
                    {
                        safeToAutoSave = false;
                        return new ConfigSaveResult { Success = false, Skipped = !userInitiated,
                            RequiresRecoveryConfirmation = true, Message = "설정 파일이 없어져 저장을 보호했습니다." };
                    }
                    AppSettings next = settings.Clone();
                    if (next.Items.Count == 0) throw new InvalidOperationException("D-Day 목록이 비어 있어 기존 설정을 보존했습니다.");
                    Normalize(next);
                    if (!userInitiated && current.Status == "ok" && !SchedulesEqual(current.Settings, next))
                    {
                        safeToAutoSave = false;
                        return new ConfigSaveResult { Success = false, Skipped = true, RequiresRecoveryConfirmation = true,
                            Message = "자동 저장 중 일정 목록 변경을 감지해 기존 파일을 보호했습니다." };
                    }
                    if (confirmRecovery) PreserveRecoveryFiles();
                    if (current.Status == "ok")
                    {
                        EnsureDailyBackup(current);
                        if (MeaningfulKey(current.Settings) != MeaningfulKey(next)) RotateBackups(current);
                    }
                    string content = Serialize(next);
                    EnsureUnchanged(current);
                    AtomicWrite(ConfigPath, content);
                    observedHash = Hash(content);
                    safeToAutoSave = true;
                    log.Info("config.save", "items=" + settings.Items.Count + ", userInitiated=" + userInitiated);
                    return new ConfigSaveResult
                    {
                        Success = true,
                        Message = "설정을 저장했습니다.",
                        Path = ConfigPath
                    };
                }
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
            if (result.Status == "ok")
            {
                result.Status = "imported";
                result.SafeToAutoSave = true;
            }
            return result;
        }

        internal void Export(AppSettings settings, string destination)
        {
            string target = Path.GetFullPath(destination);
            string directory = Path.GetFullPath(ConfigDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string name = Path.GetFileName(target);
            if (string.Equals(target, ConfigPath, StringComparison.OrdinalIgnoreCase) ||
                (target.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
                 (name.StartsWith("dday_config.bak", StringComparison.OrdinalIgnoreCase) ||
                  name.StartsWith("dday_config.daily-", StringComparison.OrdinalIgnoreCase) ||
                  target.StartsWith(Path.Combine(ConfigDirectory, "Recovery") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("운영 설정과 복구용 백업에는 내보낼 수 없습니다. 다른 파일 이름을 선택해 주세요.");
            AppSettings copy = settings.Clone();
            Normalize(copy);
            AtomicWrite(destination, Serialize(copy));
            log.Info("config.export", "completed");
        }

        private ConfigLoadResult ReadWithRetry(string path)
        {
            ConfigLoadResult result = ReadFile(path);
            foreach (int delay in new[] { 200, 500, 1000 })
            {
                if (result.Status == "ok") break;
                Thread.Sleep(delay);
                result = ReadFile(path);
            }
            return result;
        }

        private ConfigLoadResult ReadFile(string path)
        {
            string content = null;
            try
            {
                // Open directly: File.Exists also returns false for some access
                // failures, which must not be confused with a new installation.
                content = File.ReadAllText(path, new UTF8Encoding(false, true));
                Dictionary<string, Dictionary<string, string>> ini = ParseIni(content.Split('\n'));
                int schema = ValidateIntegrity(content, ini);
                Dictionary<string, string> window;
                if (!ini.TryGetValue("Window", out window)) throw new InvalidDataException("[Window] 구역이 없습니다.");

                AppSettings settings = AppSettings.CreateDefault();
                List<string> warnings = new List<string>();
                foreach (string key in new[] { "x", "y", "w", "h" })
                    if (!window.ContainsKey(key)) warnings.Add("창 설정 " + key + " 값이 누락되었습니다.");
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
                settings.HighlightUpcomingDDay = ReadBool(window, "highlight_upcoming_dday", false, warnings);
                settings.UpcomingDDayPreset = ReadEnum(window, "upcoming_dday_preset", "amber",
                    new[] { "amber", "rose", "mint", "blue", "custom" }, warnings);
                settings.UpcomingDDayBackgroundColor = ReadColor(window, "upcoming_dday_background_color", settings.UpcomingDDayBackgroundColor, warnings);
                settings.UpcomingDDayTitleColor = ReadColor(window, "upcoming_dday_title_color", settings.UpcomingDDayTitleColor, warnings);
                settings.AutoStart = ReadBool(window, "auto_start", settings.AutoStart, warnings);
                settings.TimeFormat = ReadEnum(window, "time_format", settings.TimeFormat, new[] { "12h", "24h" }, warnings);
                settings.DateFormat = ReadEnum(window, "date_format", settings.DateFormat,
                    new[] { "yyyy-mm-dd", "mm/dd/yyyy", "dd/mm/yyyy" }, warnings);
                settings.DayFormat = ReadEnum(window, "day_format", settings.DayFormat, new[] { "kor", "eng" }, warnings);
                settings.GlassStrength = ReadDouble(window, "glass_strength", settings.GlassStrength, 0.10, 1.0, warnings);
                settings.GlassLightColor = ReadColor(window, "glass_light_color", settings.GlassLightColor, warnings);
                settings.GlassRefractionColor = ReadColor(window, "glass_refraction_color", settings.GlassRefractionColor, warnings);
                settings.GlassRefractionMode = ReadEnum(window, "glass_refraction_mode", "soft", new[] { "off", "soft", "clear", "prism" }, warnings);
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
                    if (!section.Value.TryGetValue("title", out title) || string.IsNullOrWhiteSpace(title))
                    { title = "D-Day"; warnings.Add(section.Key + " 제목이 누락되었습니다."); }
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
                    SafeToAutoSave = warnings.Count == 0,
                    ContentHash = Hash(content), Content = content,
                    SavedAt = ReadSavedAt(ini, path), SchemaVersion = schema
                };
            }
            catch (FileNotFoundException) { return FailedRead(path, "missing", content); }
            catch (DirectoryNotFoundException) { return FailedRead(path, "missing", content); }
            catch (InvalidDataException ex)
            { log.Warn("config.invalid", Path.GetFileName(path)); return FailedRead(path, "error", content, ex.Message); }
            catch (IOException ex)
            { log.Error("config.unavailable", ex); return FailedRead(path, "unavailable", content); }
            catch (UnauthorizedAccessException ex)
            { log.Error("config.unavailable", ex); return FailedRead(path, "unavailable", content); }
            catch (Exception ex)
            {
                log.Error("config.read.failed", ex, "file=" + Path.GetFileName(path));
                return new ConfigLoadResult
                {
                    Settings = AppSettings.CreateDefault(),
                    Status = "error",
                    Source = path,
                    Message = ex.Message,
                    SafeToAutoSave = false, Content = content, ContentHash = content == null ? null : Hash(content)
                };
            }
        }

        private static ConfigLoadResult FailedRead(string path, string status, string content, string message = null)
        {
            return new ConfigLoadResult { Settings = AppSettings.CreateDefault(), Status = status, Source = path,
                Message = message ?? "설정 파일을 읽지 못했습니다.", SafeToAutoSave = false,
                Content = content, ContentHash = content == null ? null : Hash(content) };
        }

        private ConfigLoadResult ReadLegacyConfiguration()
        {
            legacyUnavailable = false;
            if (!allowLegacyMigration) return null;
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(Environment.CurrentDirectory, ConfigFileName),
                Path.Combine(executableDirectory, ConfigFileName)
            };
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(candidate, ConfigPath, StringComparison.OrdinalIgnoreCase)) continue;
                ConfigLoadResult result = ReadFile(candidate);
                if (result.Status == "unavailable") result = ReadWithRetry(candidate);
                if (result.Status == "ok") return result;
                if (result.Status != "missing") legacyUnavailable = true;
            }
            return null;
        }

        private FileStream OpenWriteGate()
        {
            return new FileStream(Path.Combine(ConfigDirectory, "dday_config.write.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private void EnsureUnchanged(ConfigLoadResult expected)
        {
            ConfigLoadResult latest = ReadFile(ConfigPath);
            if (latest.Status != expected.Status || latest.ContentHash != expected.ContentHash)
                throw new IOException("저장 준비 중 설정 파일이 변경되어 저장을 중지했습니다.");
        }

        internal List<ConfigLoadResult> GetBackupCandidates()
        {
            lock (gate) return GetBackupCandidatesCore();
        }

        private List<ConfigLoadResult> GetBackupCandidatesCore()
        {
            var paths = new List<string>();
            for (int i = 1; i <= BackupCount; i++) paths.Add(BackupPath(i));
            try
            {
                paths.AddRange(Directory.GetFiles(ConfigDirectory, "dday_config.daily-*.ini"));
                string recovery = Path.Combine(ConfigDirectory, "Recovery");
                if (Directory.Exists(recovery)) paths.AddRange(Directory.GetFiles(recovery, "*.ini", SearchOption.AllDirectories));
            }
            catch (IOException ex) { log.Error("config.backups.enumerate", ex); }
            catch (UnauthorizedAccessException ex) { log.Error("config.backups.enumerate", ex); }
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(ReadFile)
                .Where(result => result.Status == "ok").OrderByDescending(result => result.SavedAt).ToList();
        }

        private bool HasConfigurationHistory()
        {
            try
            {
                return Directory.GetFiles(ConfigDirectory, "dday_config*")
                    .Any(path => !path.EndsWith(".write.lock", StringComparison.OrdinalIgnoreCase)) ||
                    Directory.Exists(Path.Combine(ConfigDirectory, "Recovery"));
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        private void RotateBackups(ConfigLoadResult current)
        {
            var snapshots = new List<ConfigLoadResult> { current };
            var keys = new HashSet<string> { MeaningfulKey(current.Settings) };
            for (int i = 1; i <= BackupCount; i++)
            {
                ConfigLoadResult prior = ReadFile(BackupPath(i));
                if (prior.Status == "ok" && keys.Add(MeaningfulKey(prior.Settings))) snapshots.Add(prior);
            }
            for (int i = Math.Min(BackupCount, snapshots.Count); i >= 1; i--)
                AtomicWrite(BackupPath(i), snapshots[i - 1].Content);
        }

        private string BackupPath(int index)
        {
            return Path.Combine(ConfigDirectory, "dday_config.bak" + index + ".ini");
        }

        private void TryDailyBackup(ConfigLoadResult current)
        {
            try { EnsureDailyBackup(current); }
            catch (Exception ex) { log.Error("config.daily.failed", ex); }
        }

        private void UpgradeLegacyIntegrity(ConfigLoadResult current)
        {
            try
            {
                using (OpenWriteGate())
                {
                    EnsureDailyBackup(current);
                    EnsureUnchanged(current);
                    string content = Serialize(current.Settings);
                    AtomicWrite(ConfigPath, content);
                    current.Content = content;
                    current.ContentHash = observedHash = Hash(content);
                    current.SchemaVersion = 19;
                }
            }
            catch (Exception ex) { log.Error("config.integrity.upgrade", ex); }
        }

        private void EnsureDailyBackup(ConfigLoadResult current)
        {
            string path = Path.Combine(ConfigDirectory, "dday_config.daily-" + DateTime.Today.ToString("yyyyMMdd") + ".ini");
            ConfigLoadResult existing = ReadFile(path);
            if (existing.Status == "ok") return;
            if (existing.Status == "unavailable") throw new IOException("일별 백업에 접근하지 못해 기존 파일을 보존했습니다.");
            // Preserve a damaged daily copy before replacing it with known-good
            // data. Otherwise one damaged backup could prevent every later save.
            if (existing.Status != "missing") PreserveRecoveryFiles();
            AtomicWrite(path, current.Content, existing.Status != "missing");
            string[] old = Directory.GetFiles(ConfigDirectory, "dday_config.daily-*.ini")
                .OrderByDescending(value => value, StringComparer.OrdinalIgnoreCase).Skip(DailyBackupCount).ToArray();
            foreach (string prior in old) File.Delete(prior);
        }

        private void PreserveRecoveryFiles()
        {
            var paths = new List<string> { ConfigPath };
            for (int i = 1; i <= BackupCount; i++) paths.Add(BackupPath(i));
            paths.AddRange(Directory.GetFiles(ConfigDirectory, "dday_config.daily-*.ini"));
            string destination = Path.Combine(ConfigDirectory, "Recovery",
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            foreach (string path in paths)
            {
                try
                {
                    using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(Path.Combine(destination, Path.GetFileName(path)), FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    { source.CopyTo(output); output.Flush(true); }
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private static string MeaningfulKey(AppSettings value)
        {
            string[] lines = Serialize(value).Replace("\r\n", "\n").Split('\n');
            return Hash(string.Join("\n", lines.Where(line =>
            {
                int split = line.IndexOf('=');
                string key = split < 0 ? line.Trim() : line.Substring(0, split).Trim();
                return key != "saved_at" && key != "sha256" && key != "[Integrity]" &&
                    key != "x" && key != "y" && key != "w" && key != "h";
            })));
        }

        private static bool SchedulesEqual(AppSettings first, AppSettings second)
        {
            return first.Items.Count == second.Items.Count && first.Items.Zip(second.Items, (a, b) =>
                a.Title == b.Title && a.Date.Date == b.Date.Date && a.CalendarEventKey == b.CalendarEventKey).All(equal => equal);
        }

        private static string Hash(string content)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    content.Replace("\r\n", "\n").TrimEnd('\n')))).Replace("-", "").ToLowerInvariant();
        }

        private static int ValidateIntegrity(string content, Dictionary<string, Dictionary<string, string>> ini)
        {
            Dictionary<string, string> meta;
            string text;
            int schema = 0;
            if (ini.TryGetValue("Meta", out meta) && meta.TryGetValue("schema_version", out text))
                if (!int.TryParse(text, out schema) || schema < 0 || schema > 19)
                    throw new InvalidDataException("지원하지 않는 설정 형식입니다. 원본은 보존합니다.");
            if (schema < 19) return schema; // Complete legacy data can be upgraded after a separate backup.
            Dictionary<string, string> integrity;
            string expected;
            int count;
            string canonical = content.Replace("\r\n", "\n");
            int marker = canonical.IndexOf("\n[Integrity]\n", StringComparison.Ordinal);
            if (marker < 0 || !ini.TryGetValue("Integrity", out integrity) || !integrity.TryGetValue("sha256", out expected) ||
                !string.Equals(Hash(canonical.Substring(0, marker)), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("설정 파일 무결성 검사를 통과하지 못했습니다.");
            if (!meta.TryGetValue("dday_count", out text) || !int.TryParse(text, out count) || count < 1 ||
                count != ini.Count(pair => pair.Key.StartsWith("DDay-", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("저장된 D-Day 개수가 일치하지 않습니다.");
            return schema;
        }

        private static DateTimeOffset ReadSavedAt(Dictionary<string, Dictionary<string, string>> ini, string path)
        {
            Dictionary<string, string> meta;
            string value;
            DateTimeOffset saved;
            if (ini.TryGetValue("Meta", out meta) && meta.TryGetValue("saved_at", out value) &&
                DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out saved)) return saved;
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path));
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
                    if (name.Length == 0 || result.ContainsKey(name)) throw new InvalidDataException("설정 구역이 비어 있거나 중복됩니다.");
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[name] = current;
                    continue;
                }
                if (current == null) throw new InvalidDataException("설정 파일의 시작 구역이 잘못되었습니다.");
                int split = line.IndexOf('=');
                if (split < 0) split = line.IndexOf(':');
                if (split <= 0 || line.StartsWith("[")) throw new InvalidDataException("설정 파일의 일부가 잘렸거나 잘못되었습니다.");
                string key = line.Substring(0, split).Trim();
                if (current.ContainsKey(key)) throw new InvalidDataException("설정 항목이 중복됩니다.");
                current[key] = line.Substring(split + 1).Trim();
            }
            return result;
        }

        private static string Serialize(AppSettings value)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("[Meta]");
            text.AppendLine("schema_version = 19");
            text.AppendLine("saved_at = " + DateTimeOffset.Now.ToString("o"));
            Write(text, "dday_count", value.Items.Count);
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
            Write(text, "highlight_upcoming_dday", value.HighlightUpcomingDDay);
            Write(text, "upcoming_dday_preset", value.UpcomingDDayPreset);
            Write(text, "upcoming_dday_background_color", value.UpcomingDDayBackgroundColor);
            Write(text, "upcoming_dday_title_color", value.UpcomingDDayTitleColor);
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
            Write(text, "glass_refraction_color", value.GlassRefractionColor);
            Write(text, "glass_refraction_mode", value.GlassRefractionMode);
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
            string checksum = Hash(text.ToString());
            text.AppendLine().AppendLine("[Integrity]");
            Write(text, "sha256", checksum);
            return text.ToString();
        }

        private static void Write(StringBuilder text, string key, object value)
        {
            text.Append(key).Append(" = ").AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void AtomicWrite(string path, string content, bool replaceExisting = true)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    16384, FileOptions.WriteThrough))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (replaceExisting && File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void Normalize(AppSettings value)
        {
            value.GlassRefractionColor = GlassRefraction.NormalizeColor(value.GlassRefractionColor);
            value.GlassRefractionMode = GlassRefraction.NormalizeMode(value.GlassRefractionMode);
            value.UpcomingDDayPreset = DDayHighlightPolicy.NormalizePreset(value.UpcomingDDayPreset);
            value.UpcomingDDayBackgroundColor = DDayHighlightPolicy.NormalizeColor(value.UpcomingDDayBackgroundColor, "#C9A96B");
            value.UpcomingDDayTitleColor = DDayHighlightPolicy.NormalizeColor(value.UpcomingDDayTitleColor, "#F4DCA5");
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
