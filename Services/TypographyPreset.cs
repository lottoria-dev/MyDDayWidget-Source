using System;
namespace DDay3.Services
{
    internal sealed class TypographyPreset
    {
        internal string Id { get; private set; }
        internal string Category { get; private set; }
        internal string Name { get; private set; }
        private readonly string numberFamily, textFamily, titleWeight;
        private TypographyPreset(string id, string category, string name, string numbers, string text, string weight = "Normal")
        {
            Id = id; Category = category; Name = name;
            numberFamily = numbers; textFamily = text; titleWeight = weight;
        }
        internal string Family(string role)
        {
            if (Id == "clock-mono") return role == "time" ? "Consolas" : TypographySize.DefaultFamily(role);
            return role == "time" || role == "dday_count" || role == "calendar" ? numberFamily : textFamily;
        }
        internal string Weight(string role) { return role == "dday_title" ? titleWeight : "Normal"; }
        internal static readonly TypographyPreset[] All =
        {
            new TypographyPreset("clock-mono", "시계 우선 추천", "Consolas · 고정폭 시계", "Consolas", "Malgun Gothic"),
            new TypographyPreset("en-mono", "영문 추천", "Consolas · 고정폭 숫자", "Consolas", "Segoe UI"),
            new TypographyPreset("en-balanced", "영문 추천", "Tahoma · 또렷한 숫자", "Tahoma", "Segoe UI"),
            new TypographyPreset("en-modern", "영문 추천", "Segoe UI · 부드러운 균형", "Segoe UI", "Segoe UI", "Medium"),
            new TypographyPreset("ko-modern", "한글 추천", "맑은 고딕 · 현대적 기본", "Tahoma", "Malgun Gothic"),
            new TypographyPreset("ko-compact", "한글 추천", "돋움 · 간결한 구성", "Tahoma", "Dotum"),
            new TypographyPreset("ko-book", "한글 추천", "바탕 · 차분한 명조", "Georgia", "Batang")
        };
        internal static readonly string[] ClockFamilies = { "Consolas", "Cascadia Mono", "Courier New" };
        internal static TypographyPreset Find(string id)
        {
            foreach (TypographyPreset preset in All)
                if (string.Equals(preset.Id, id, StringComparison.Ordinal)) return preset;
            return All[0];
        }
    }
}
