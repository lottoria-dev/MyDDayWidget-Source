using System;
using System.Security.Cryptography;
using System.Text;

namespace DDay3.Models
{
    internal sealed class CalendarEvent
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
        public bool OccursOn(DateTime day)
        {
            // iCalendar end is exclusive. A midnight ending must not mark the following day.
            DateTime finalDay = End > Start ? End.AddTicks(-1).Date : Start.Date;
            return Start.Date <= day.Date && finalDay >= day.Date;
        }
        internal CalendarEvent Copy() { return (CalendarEvent)MemberwiseClone(); }
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

}
