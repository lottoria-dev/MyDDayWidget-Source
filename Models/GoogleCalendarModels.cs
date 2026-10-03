using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DDay3.Models
{
    internal sealed class GoogleCalendarEntry
    {
        public GoogleCalendarEntry() { }
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    internal sealed class GoogleCalendarEvent
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string CalendarName { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool AllDay { get; set; }
        public bool HasTitle { get; set; }
        public bool IsSelected { get; set; }
        public bool AlreadyImported { get; set; }
        public bool CanImport { get { return HasTitle && !AlreadyImported; } }
        public string Details
        {
            get
            {
                string when = AllDay ? Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " · 종일"
                    : Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (PC 시간)";
                return when + " · " + CalendarName + (AlreadyImported ? " · 이미 등록됨" : !HasTitle ? " · 제목 비공개" : "");
            }
        }
        public bool OccursOn(DateTime day)
        {
            // Google end is exclusive. A midnight ending must not mark the following day.
            DateTime finalDay = End > Start ? End.AddTicks(-1).Date : Start.Date;
            return Start.Date <= day.Date && finalDay >= day.Date;
        }
        internal GoogleCalendarEvent Copy() { return (GoogleCalendarEvent)MemberwiseClone(); }
        internal static string SourceKey(string calendarId, string eventId)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(calendarId + "\n" + eventId))).Replace("-", "").ToLowerInvariant();
        }
        internal static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return "";
            foreach (char c in value) if (!Uri.IsHexDigit(c)) return "";
            return value.ToLowerInvariant();
        }
    }

    internal sealed class GoogleEventResult
    {
        internal List<GoogleCalendarEvent> Events { get; } = new List<GoogleCalendarEvent>();
        internal List<string> Warnings { get; } = new List<string>();
        internal string Status { get; set; }
    }
}
