using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using DDay3.Models;

namespace DDay3.Services
{
    internal static class GoogleCalendarData
    {
        internal static JavaScriptSerializer Serializer()
        { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 32 }; }
        internal static Dictionary<string, object> Object(string json)
        {
            var value = Serializer().DeserializeObject(json) as Dictionary<string, object>;
            if (value == null) throw new InvalidDataException("Google 응답 형식을 확인할 수 없습니다.");
            return value;
        }
        internal static string Text(IDictionary<string, object> value, string key)
        { object entry; return value != null && value.TryGetValue(key, out entry) ? entry as string ?? "" : ""; }
        internal static Dictionary<string, object> Child(IDictionary<string, object> value, string key)
        { object entry; return value != null && value.TryGetValue(key, out entry) ? entry as Dictionary<string, object> : null; }
        internal static IEnumerable<Dictionary<string, object>> Items(IDictionary<string, object> value)
        {
            object entry;
            var items = value.TryGetValue("items", out entry) ? entry as IEnumerable : null;
            if (items == null) yield break;
            foreach (object item in items)
            { var row = item as Dictionary<string, object>; if (row != null) yield return row; }
        }
        internal static string Clean(string text, int max = 160)
        {
            string clean = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }
        internal static GoogleCalendarEvent Event(Dictionary<string, object> row, GoogleCalendarEntry calendar, TimeZoneInfo zone)
        {
            if (Text(row, "status") == "cancelled" || Text(row, "id").Length == 0) return null;
            var start = Child(row, "start"); var end = Child(row, "end");
            DateTime begins, ends;
            bool allDay = Text(start, "date").Length != 0;
            if (allDay)
            {
                if (!DateTime.TryParseExact(Text(start, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out begins)
                    || !DateTime.TryParseExact(Text(end, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out ends)) return null;
            }
            else
            {
                DateTimeOffset first, last;
                if (!TryOffset(Text(start, "dateTime"), out first) || !TryOffset(Text(end, "dateTime"), out last)) return null;
                begins = TimeZoneInfo.ConvertTime(first, zone).DateTime;
                ends = TimeZoneInfo.ConvertTime(last, zone).DateTime;
                if (last < first) return null;
            }
            if (allDay && ends <= begins) return null;
            string title = Clean(Text(row, "summary"));
            return new GoogleCalendarEvent { Key = GoogleCalendarEvent.SourceKey(calendar.Id, Text(row, "id")),
                Title = title.Length == 0 ? "제목을 볼 수 없는 일정" : title, HasTitle = title.Length > 0,
                CalendarName = calendar.Name, Start = begins, End = ends, AllDay = allDay };
        }
        private static bool TryOffset(string value, out DateTimeOffset date)
        {
            // Requests specify timeZone=UTC; never interpret an offset-free value as PC time.
            date = default(DateTimeOffset);
            return value.Length >= 20 && (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                || value.LastIndexOf('+') > 10 || value.LastIndexOf('-') > 10)
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
