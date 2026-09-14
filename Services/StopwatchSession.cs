using System;
using System.Diagnostics;
using System.Globalization;

namespace DDay3.Services
{
    internal sealed class StopwatchSession
    {
        private readonly Stopwatch watch = new Stopwatch();
        internal bool IsRunning { get { return watch.IsRunning; } }
        internal string Text { get { return Format(watch.Elapsed); } }
        internal void Toggle() { if (watch.IsRunning) watch.Stop(); else watch.Start(); }
        internal void Pause() { watch.Stop(); }
        internal void Reset() { watch.Reset(); }
        internal static string Format(TimeSpan elapsed)
        {
            return ((long)elapsed.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" +
                elapsed.Minutes.ToString("00") + ":" + elapsed.Seconds.ToString("00") + "." + (elapsed.Milliseconds / 100);
        }
    }
}
