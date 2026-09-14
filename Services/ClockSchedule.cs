using System;

namespace DDay3.Services
{
    internal static class ClockSchedule
    {
        // Re-evaluate against wall time after every callback; a delayed frame never accumulates drift.
        internal static TimeSpan NextDelay(DateTime now, bool runningStopwatch)
        {
            if (runningStopwatch) return TimeSpan.FromMilliseconds(100);
            double remaining = (TimeSpan.TicksPerSecond - now.Ticks % TimeSpan.TicksPerSecond) / (double)TimeSpan.TicksPerMillisecond;
            return TimeSpan.FromMilliseconds(Math.Max(16, Math.Ceiling(remaining) + 2));
        }
    }
}
