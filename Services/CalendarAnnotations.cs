using System;
using System.Collections.Generic;
using System.Text;
using DDay3.Models;

namespace DDay3.Services
{
    internal sealed class CalendarAnnotations
    {
        private readonly Dictionary<DateTime, List<string>> titles = new Dictionary<DateTime, List<string>>();
        internal void Replace(IEnumerable<DDayItem> items)
        {
            titles.Clear();
            foreach (DDayItem item in items)
            {
                List<string> dayTitles;
                if (!titles.TryGetValue(item.Date.Date, out dayTitles))
                {
                    dayTitles = new List<string>();
                    titles.Add(item.Date.Date, dayTitles);
                }
                dayTitles.Add(item.Title ?? string.Empty);
            }
        }
        internal bool Contains(DateTime date) { return titles.ContainsKey(date.Date); }
        internal string Describe(DateTime date, bool english = false, string dateFormat = "yyyy-mm-dd")
        {
            StringBuilder text = new StringBuilder((english ? "Solar " : "양력 ") + DateDisplayFormat.Format(date, dateFormat));
            string lunar;
            text.AppendLine().Append(LunarDateService.TryFormat(date.Date, out lunar, english, dateFormat) ? lunar :
                (english ? "Lunar: outside supported range" : "음력: 지원 범위 밖"));
            List<string> dayTitles;
            if (titles.TryGetValue(date.Date, out dayTitles))
            {
                foreach (string title in dayTitles) text.AppendLine().Append("• ").Append(title);
            }
            return text.ToString();
        }
    }
}
