using System;

namespace DDay3.Services
{
    internal sealed class ScheduleCarousel
    {
        private int wheelRemainder;
        internal int Index { get; private set; }
        internal static int VisibleCount(int total, int requested)
        {
            return Math.Min(Math.Max(0, total), Math.Max(1, Math.Min(10, requested)));
        }
        internal static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;
            return (int)(((long)index % count + count) % count);
        }
        internal void Reset() { Index = 0; wheelRemainder = 0; }
        internal bool Move(int steps, int count)
        {
            int next = Wrap(Index + steps, count);
            bool changed = next != Index;
            Index = next;
            return changed;
        }
        internal bool Wheel(int delta, int count)
        {
            if (count < 2) { Reset(); return false; }
            wheelRemainder += delta;
            int steps = wheelRemainder / 120;
            wheelRemainder %= 120;
            return steps != 0 && Move(-steps, count);
        }
    }
}
