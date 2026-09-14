using System.Linq;

namespace DDay3.Services
{
    internal sealed class GlassDepthPreset
    {
        internal string Id { get; private set; }
        internal string Name { get; private set; }
        internal int Background { get; private set; }
        internal int Clock { get; private set; }
        internal int DDay { get; private set; }
        private GlassDepthPreset(string id, string name, int background, int clock, int dday)
        { Id = id; Name = name; Background = background; Clock = clock; DDay = dday; }

        internal static readonly GlassDepthPreset[] All =
        {
            new GlassDepthPreset("subtle", "은은하게", 35, 20, 20),
            new GlassDepthPreset("raised", "전체 양각", 70, 60, 60),
            new GlassDepthPreset("mixed", "배경 양각·카드 음각", 55, -45, -45),
            new GlassDepthPreset("inset", "전체 음각", -45, -55, -55),
            new GlassDepthPreset("flat", "평면", 0, 0, 0)
        };

        internal static GlassDepthPreset Match(int background, int clock, int dday)
        { return All.FirstOrDefault(p => p.Background == background && p.Clock == clock && p.DDay == dday); }
    }
}
