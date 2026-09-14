using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DDay3.Services
{
    internal sealed class HolidaySyncResult
    {
        internal bool Success { get; set; }
        internal bool HasData { get; set; }
        internal string Message { get; set; }
        internal string EnglishMessage { get; set; }
    }

    internal sealed class KoreanHolidayService : IDisposable
    {
        private readonly string directory;
        private readonly SpecialDateKind kind;
        private string Label { get { return kind == SpecialDateKind.SolarTerm ? "24절기" : "공휴일"; } }
        private string EnglishLabel { get { return kind == SpecialDateKind.SolarTerm ? "solar terms" : "holidays"; } }
        private readonly HttpClient client;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, List<HolidayEntry>> years = new Dictionary<int, List<HolidayEntry>>();
        private readonly Dictionary<int, DateTime> attempts = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, HolidaySyncResult> outcomes = new Dictionary<int, HolidaySyncResult>();
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        internal KoreanHolidayService(string configDirectory, HttpMessageHandler handler = null, SpecialDateKind kind = SpecialDateKind.Holiday)
        {
            this.kind = kind;
            directory = Path.Combine(configDirectory, "HolidayCache");
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024 };
        }
        private string CachePath(int year) { return Path.Combine(directory, (kind == SpecialDateKind.SolarTerm ? "KR-Terms-" : "KR-") + year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + ".xml"); }

        // Calls originate on the WPF dispatcher; await continuations preserve that context.
        internal void LoadCached(int year)
        {
            if (years.ContainsKey(year)) return;
            try
            {
                string file = CachePath(year);
                if (File.Exists(file) && new FileInfo(file).Length <= 1024 * 1024)
                {
                    List<HolidayEntry> parsed = HolidayData.Parse(File.ReadAllText(file), year, kind);
                    if (kind == SpecialDateKind.SolarTerm) HolidayData.SolarTermCache(parsed);
                    years[year] = parsed;
                }
            }
            catch (Exception) { /* Invalid/unreadable cache is never presented as verified data. */ }
        }

        internal List<HolidayEntry> Snapshot() { return years.Values.SelectMany(x => x).ToList(); }

        internal async Task<HolidaySyncResult> RefreshAsync(int year, string key, bool force)
        {
            await gate.WaitAsync();
            try
            {
                LoadCached(year);
                bool hasData = years.ContainsKey(year);
                DateTime now = DateTime.UtcNow;
                if (!force && hasData && File.GetLastWriteTimeUtc(CachePath(year)) <= now &&
                    now - File.GetLastWriteTimeUtc(CachePath(year)) < TimeSpan.FromDays(7))
                    return Result(true, true, year + "년 " + Label + " · 저장 자료", year + " " + EnglishLabel + " · cached data");
                if (string.IsNullOrWhiteSpace(key))
                    return Result(false, hasData, hasData ? "인증키 없음 · 저장 자료 표시" : Label + " 미연동 · 설정에서 인증키 입력",
                        hasData ? "No API key · using cached " + EnglishLabel : EnglishLabel + " unavailable · enter an API key in Settings");
                DateTime last;
                if (!force && attempts.TryGetValue(year, out last) && now - last < TimeSpan.FromMinutes(30))
                    return outcomes[year];
                attempts[year] = now;
                try
                {
                    // Solar-term API documents solMonth as required: load all twelve months
                    // within one bounded transaction. A partial year never replaces a cache.
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(45));
                        string xml;
                        if (kind == SpecialDateKind.SolarTerm)
                        {
                            var annual = new List<HolidayEntry>();
                            for (int month = 1; month <= 12; month++)
                            {
                                string monthly = await DownloadAsync(HolidayData.RequestUri(year, key, kind, month), timeout.Token);
                                List<HolidayEntry> entries = HolidayData.Parse(monthly, year, kind);
                                if (entries.Any(x => x.Date.Month != month)) throw new FormatException("Wrong month");
                                annual.AddRange(entries);
                            }
                            xml = HolidayData.SolarTermCache(annual);
                        }
                        else xml = await DownloadAsync(HolidayData.RequestUri(year, key), timeout.Token);
                        List<HolidayEntry> parsed = HolidayData.Parse(xml, year, kind);
                        // Publish only a fully validated year, then atomically replace the disk cache.
                        years[year] = parsed;
                        try
                        {
                            Directory.CreateDirectory(directory);
                            string path = CachePath(year), temporary = path + ".tmp";
                            try
                            {
                                File.WriteAllText(temporary, xml);
                                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                            }
                            finally { if (File.Exists(temporary)) File.Delete(temporary); }
                        }
                        catch (Exception)
                        {
                            return outcomes[year] = Result(true, true, year + "년 수신 완료 · 캐시 저장 실패(이번 실행만 표시)",
                                year + " loaded · cache write failed (session only)");
                        }
                        return outcomes[year] = Result(true, true, year + "년 " + Label + " " + parsed.Count + "건 갱신 완료",
                            year + " " + EnglishLabel + " updated (" + parsed.Count + ")");
                    }
                }
                catch (Exception)
                {
                    // Never expose exception text: HTTP exceptions may contain the credential-bearing URL.
                    return outcomes[year] = Result(false, hasData,
                        "연동 실패: 인증키·활용신청·연도·인터넷 확인" + (hasData ? " · 이전 자료 표시" : " · 자료 없음"),
                        "Sync failed: check API key, approval, year and network" + (hasData ? " · using old data" : " · no data"));
                }
            }
            finally { gate.Release(); }
        }
        private async Task<string> DownloadAsync(Uri uri, CancellationToken token)
        {
            using (var response = await client.GetAsync(uri, token))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        private static HolidaySyncResult Result(bool success, bool data, string message, string english)
        {
            return new HolidaySyncResult { Success = success, HasData = data, Message = message, EnglishMessage = english };
        }
        public void Dispose() { shutdown.Cancel(); client.Dispose(); }
    }
}
