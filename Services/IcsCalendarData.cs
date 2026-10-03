using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IcsCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;
using DDay3.Models;

namespace DDay3.Services
{
    internal sealed class IcsRefreshOption
    {
        public int Minutes { get; private set; }
        public string Title { get; private set; }
        internal IcsRefreshOption(int minutes, string title) { Minutes = minutes; Title = title; }
    }
    internal sealed class IcsCalendarSnapshot
    {
        private static readonly IcsRefreshOption[] intervals = {
            new IcsRefreshOption(0, "수동"), new IcsRefreshOption(15, "15분"), new IcsRefreshOption(30, "30분"),
            new IcsRefreshOption(60, "1시간"), new IcsRefreshOption(180, "3시간"),
            new IcsRefreshOption(360, "6시간"), new IcsRefreshOption(1440, "하루") };
        public IEnumerable<IcsRefreshOption> RefreshChoices { get { return intervals; } }
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string Content { get; set; } = "";
        public int EventCount { get; set; }
        public DateTime ImportedUtc { get; set; } = DateTime.UtcNow;
        public string SubscriptionUrl { get; set; } = "";
        public int RefreshMinutes { get; set; } = 30;
        public DateTime LastAttemptUtc { get; set; } = DateTime.MinValue;
        public string RefreshError { get; set; } = "";
        public bool IsSubscription { get { return !string.IsNullOrEmpty(SubscriptionUrl); } }
        public string Label { get { return Name + " · " + EventCount + "개"; } }
        public string ImportedText { get { return (IsSubscription ? "URL 구독 · 갱신: " : "파일 · 가져온 날짜: ")
            + ImportedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            + (IsSubscription ? (RefreshMinutes == 0 ? " · 수동" : " · " + RefreshMinutes + "분마다") : "")
            + (RefreshError.Length == 0 ? "" : "\n" + RefreshError); } }
        internal IcsCalendarSnapshot Copy() { return (IcsCalendarSnapshot)MemberwiseClone(); }
    }

    internal static class IcsCalendarData
    {
        internal const int MaximumFileBytes = 5 * 1024 * 1024;
        internal const int MaximumEvents = 10000;
        private static readonly HashSet<string> EventFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "UID", "SUMMARY", "DTSTART", "DTEND", "DURATION", "RRULE", "RDATE", "EXDATE", "RECURRENCE-ID",
          "STATUS", "SEQUENCE", "DTSTAMP", "LAST-MODIFIED" };
        private static readonly HashSet<string> ZoneFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "TZID", "DTSTART", "TZOFFSETFROM", "TZOFFSETTO", "RRULE", "RDATE", "EXDATE" };

        internal static IcsCalendarSnapshot ReadFile(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".ics", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(".ics 일정 파일을 선택하세요. ZIP은 먼저 압축을 풀어 주세요.");
            if (new FileInfo(path).Length > MaximumFileBytes) throw new InvalidDataException("ICS 파일은 5MB까지 가져올 수 있습니다.");
            using (var stream = File.OpenRead(path))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
                return Parse(reader.ReadToEnd(), Path.GetFileNameWithoutExtension(path));
        }

        internal static IcsCalendarSnapshot Parse(string text, string fallbackName)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > MaximumFileBytes) throw new InvalidDataException("ICS 파일 크기를 확인하세요.");
            var clean = new StringBuilder(); var stack = new Stack<string>();
            string name = ""; int calendars = 0, events = 0;
            foreach (string line in Unfold(text))
            {
                int colon = ValueSeparator(line);
                if (colon < 1) throw new InvalidDataException("ICS 속성 형식이 올바르지 않습니다.");
                string field = line.Substring(0, colon).Split(';')[0].ToUpperInvariant();
                string value = line.Substring(colon + 1);
                if (field == "BEGIN")
                {
                    string component = value.ToUpperInvariant();
                    if (component == "VCALENDAR") { if (stack.Count != 0 || ++calendars != 1) throw new InvalidDataException("파일마다 하나의 캘린더만 가져올 수 있습니다."); }
                    else if (stack.Count == 0) throw new InvalidDataException("VCALENDAR 시작이 없습니다.");
                    if (component == "VEVENT" && ++events > MaximumEvents) throw new InvalidDataException("파일당 일정은 10,000개까지 가져올 수 있습니다.");
                    stack.Push(component);
                    if (Retained(stack)) clean.AppendLine(line);
                }
                else if (field == "END")
                {
                    if (stack.Count == 0 || !string.Equals(stack.Peek(), value, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ICS 구간이 닫히지 않았습니다.");
                    if (Retained(stack)) clean.AppendLine(line);
                    stack.Pop();
                }
                else if (stack.Count > 0 && Retained(stack))
                {
                    string component = stack.Peek();
                    bool keep = component == "VEVENT" ? EventFields.Contains(field)
                        : component == "VCALENDAR" ? new[] { "VERSION", "PRODID", "CALSCALE", "X-WR-CALNAME", "X-WR-TIMEZONE" }.Contains(field)
                        : ZoneFields.Contains(field);
                    if (keep)
                    {
                        if (field == "RRULE" && (value.IndexOf("FREQ=SECONDLY", StringComparison.OrdinalIgnoreCase) >= 0
                            || value.IndexOf("FREQ=MINUTELY", StringComparison.OrdinalIgnoreCase) >= 0
                            || value.IndexOf("FREQ=HOURLY", StringComparison.OrdinalIgnoreCase) >= 0
                            || Regex.IsMatch(value, @"(?:^|;)BY(?:SECOND|MINUTE|HOUR)=", RegexOptions.IgnoreCase)))
                            throw new InvalidDataException("시간·분·초 단위 반복 일정은 지원하지 않습니다. 날짜 단위 캘린더를 선택하세요.");
                        if (component == "VCALENDAR" && field == "X-WR-CALNAME") name = DecodeText(value);
                        clean.AppendLine(line);
                    }
                }
            }
            if (calendars != 1 || stack.Count != 0) throw new InvalidDataException("완전한 ICS 캘린더 파일을 선택하세요.");
            string content = clean.ToString();
            var calendar = IcsCalendar.Load(content);
            if (calendar == null || calendar.Events.Count != events) throw new InvalidDataException("일정 파일을 읽을 수 없습니다.");
            foreach (var entry in calendar.Events)
                if (entry.DtStart == null || string.IsNullOrWhiteSpace(entry.Uid)) throw new InvalidDataException("일정에 날짜 또는 식별자가 없습니다.");
            return new IcsCalendarSnapshot { Content = content, Name = CleanTitle(string.IsNullOrWhiteSpace(name) ? fallbackName : name), EventCount = events };
        }

        private static bool Retained(IEnumerable<string> stack)
        {
            return stack.All(s => s == "VCALENDAR" || s == "VEVENT" || s == "VTIMEZONE" || s == "STANDARD" || s == "DAYLIGHT");
        }
        private static IEnumerable<string> Unfold(string text)
        {
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string pending = null, line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    if (line[0] == ' ' || line[0] == '\t')
                    {
                        if (pending == null) throw new InvalidDataException("ICS 줄 접힘 형식이 올바르지 않습니다.");
                        pending += line.Substring(1);
                    }
                    else { if (pending != null) yield return pending; pending = line; }
                    if (pending.Length > 131072) throw new InvalidDataException("ICS 속성이 너무 깁니다.");
                }
                if (pending != null) yield return pending;
            }
        }
        private static int ValueSeparator(string line)
        {
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            { if (line[index] == '"') quoted = !quoted; else if (!quoted && line[index] == ':') return index; }
            return -1;
        }
        private static string DecodeText(string value)
        {
            return Regex.Replace(value, @"\\([nN,;\\])", m => m.Groups[1].Value.Equals("n", StringComparison.OrdinalIgnoreCase) ? " " : m.Groups[1].Value);
        }
        internal static string CleanTitle(string value)
        {
            string clean = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length > 160 ? clean.Substring(0, 160) : clean;
        }

        internal static List<CalendarEvent> Events(IcsCalendarSnapshot snapshot, DateTime begin, DateTime end)
        {
            var result = new List<CalendarEvent>();
            var calendar = IcsCalendar.Load(snapshot.Content);
            foreach (var occurrence in calendar.GetOccurrences<IcalEvent>(begin, end))
            {
                var source = occurrence.Source as IcalEvent;
                if (source == null || string.Equals(source.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
                string title = CleanTitle(source.Summary);
                if (title.Length == 0) continue;
                var period = occurrence.Period;
                bool allDay = !period.StartTime.HasTime;
                DateTime start = allDay ? period.StartTime.Value.Date : period.StartTime.AsSystemLocal;
                DateTime finish = period.EndTime == null ? start : allDay ? period.EndTime.Value.Date : period.EndTime.AsSystemLocal;
                if (finish <= start && allDay && start.Date < DateTime.MaxValue.Date) finish = start.Date.AddDays(1);
                if (start >= end || (finish > start ? finish <= begin : start < begin)) continue;
                var original = source.RecurrenceId ?? period.StartTime;
                bool recurring = source.RecurrenceId != null || source.RecurrenceRules.Count > 0 || source.RecurrenceDates.Count > 0;
                string instance = !recurring ? "" : original.HasTime
                    ? original.AsSystemLocal.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
                    : original.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                result.Add(new CalendarEvent { Key = CalendarEvent.SourceKey("ics", source.Uid + "\n" + instance), Title = title,
                    HasTitle = true, CalendarName = snapshot.Name, Start = start, End = finish, AllDay = allDay });
                if (result.Count > 10000) throw new InvalidDataException("표시 기간의 일정이 너무 많습니다.");
            }
            return result;
        }
    }
}
