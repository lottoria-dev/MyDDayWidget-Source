using System;

namespace DDay3.Models
{
    internal sealed class DDayItem
    {
        internal string Title { get; set; }
        internal DateTime Date { get; set; }

        internal DDayItem Clone()
        {
            return new DDayItem { Title = Title, Date = Date.Date };
        }
    }
}
