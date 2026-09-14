using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DDay3.Models;

namespace DDay3.Services
{
    internal static class ScheduleOrdering
    {
        // A one-shot stable sort. Saving the list commits its order; time never silently reorders it.
        internal static List<DDayItem> Sort(IEnumerable<DDayItem> items, string mode, DateTime today)
        {
            switch (mode)
            {
                case "date_asc": return items.OrderBy(x => x.Date.Date).ToList();
                case "date_desc": return items.OrderByDescending(x => x.Date.Date).ToList();
                case "nearest": return items.OrderBy(x => Math.Abs((x.Date.Date - today.Date).Days)).ToList();
                case "title": return items.OrderBy(x => x.Title ?? string.Empty,
                    StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true)).ToList();
                default: return items.ToList();
            }
        }
    }
}
