using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DDay3.Models;

namespace DDay3.Services
{
    // Called on the UI dispatcher. Network awaits do not block it; generation checks discard old accounts/results.
    internal sealed class GoogleCalendarService : IDisposable
    {
        private readonly GoogleCalendarStore store;
        private readonly GoogleHttp http;
        private readonly SemaphoreSlim tokenGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private GoogleConnection state;
        private string accessToken = "";
        private DateTime accessExpires;
        private int generation;
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private sealed class CacheEntry
        {
            internal DateTime Updated;
            internal List<GoogleCalendarEvent> Events;
        }
        internal string LoadMessage { get; private set; } = "";
        internal bool Connected { get { return !string.IsNullOrEmpty(state.RefreshToken); } }
        internal bool Enabled { get { return Connected && state.Calendars.Count > 0; } }
        internal int SelectedCount { get { return state.Calendars.Count; } }
        internal event EventHandler Changed;

        internal GoogleCalendarService(string directory, GoogleHttp transport = null)
        {
            http = transport ?? new GoogleHttp();
            store = new GoogleCalendarStore(directory);
            try { state = store.Read(); }
            catch { state = new GoogleConnection(); LoadMessage = "저장된 Google 연결을 읽지 못했습니다. 이 Windows 계정에서 다시 연결하세요."; }
        }
        internal Task Connect(string credentialsPath, CancellationToken token)
        {
            return ConnectClient(GoogleCalendarStore.ReadClient(credentialsPath), token);
        }
        internal Task ConnectOfficial(CancellationToken token)
        {
            return ConnectClient(GoogleOAuthClient.ReadOfficial(), token);
        }
        private async Task ConnectClient(GoogleConnection next, CancellationToken token)
        {
            int expected = generation;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                var response = await GoogleOAuth.Connect(next, http, linked.Token);
                EnsureCurrent(expected, linked.Token);
                next.RefreshToken = GoogleCalendarData.Text(response, "refresh_token");
                store.Save(next); // A failed save must leave the previous account usable.
                state = next;
                SetAccess(response);
                ChangedState();
            }
        }
        internal async Task<bool> Disconnect(CancellationToken token)
        {
            string refresh = state.RefreshToken;
            // Delete locally first. A network failure must never retain a connection the user removed.
            store.Delete();
            state = new GoogleConnection(); accessToken = "";
            ChangedState();
            if (refresh.Length == 0) return true;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                try { await http.Revoke(refresh, linked.Token); return true; }
                catch (Exception) { return false; } // The UI offers Google's permission page for offline failures.
            }
        }
        internal void SelectCalendars(IEnumerable<GoogleCalendarEntry> calendars)
        {
            if (!Connected) throw new InvalidOperationException("Google 계정에 먼저 연결하세요.");
            var chosen = calendars.Where(c => c.IsSelected).GroupBy(c => c.Id).Select(g => g.First()).ToList();
            if (chosen.Count > 20) throw new InvalidOperationException("한 번에 최대 20개 캘린더를 선택할 수 있습니다.");
            var next = new GoogleConnection { ClientId = state.ClientId, ClientSecret = state.ClientSecret, RefreshToken = state.RefreshToken,
                Calendars = chosen.Select(c => new GoogleCalendarEntry { Id = c.Id, Name = c.Name, IsSelected = true }).ToList() };
            store.Save(next); state = next; ChangedState();
        }
        private void ChangedState()
        {
            generation++; cache.Clear(); LoadMessage = "";
            Changed?.Invoke(this, EventArgs.Empty);
        }
        private void EnsureCurrent(int expected, CancellationToken token)
        { token.ThrowIfCancellationRequested(); if (expected != generation) throw new OperationCanceledException(); }
        private void SetAccess(Dictionary<string, object> response)
        {
            string value = GoogleCalendarData.Text(response, "access_token");
            if (value.Length == 0) throw new InvalidOperationException("Google 연결을 갱신하지 못했습니다. 다시 연결하세요.");
            object seconds; int duration;
            if (!response.TryGetValue("expires_in", out seconds) || !int.TryParse(Convert.ToString(seconds, CultureInfo.InvariantCulture), out duration)) duration = 3600;
            accessToken = value; accessExpires = DateTime.UtcNow.AddSeconds(Math.Max(60, Math.Min(86400, duration)) - 30);
        }
        private async Task<string> Access(int expected, CancellationToken token)
        {
            await tokenGate.WaitAsync(token);
            try
            {
                EnsureCurrent(expected, token);
                if (accessToken.Length != 0 && accessExpires > DateTime.UtcNow) return accessToken;
                var values = new Dictionary<string, string> { { "client_id", state.ClientId }, { "refresh_token", state.RefreshToken }, { "grant_type", "refresh_token" } };
                if (!string.IsNullOrEmpty(state.ClientSecret)) values.Add("client_secret", state.ClientSecret);
                Dictionary<string, object> response;
                try { response = await http.Token(values, token); }
                catch (GoogleApiException e) when (e.Status == HttpStatusCode.BadRequest)
                { throw new InvalidOperationException("Google 연결을 갱신하지 못했습니다. 연결 창에서 다시 로그인하세요."); }
                EnsureCurrent(expected, token); SetAccess(response); return accessToken;
            }
            finally { tokenGate.Release(); }
        }
        private async Task<Dictionary<string, object>> Get(string path, int expected, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                string access = await Access(expected, token);
                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/" + path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
                try
                {
                    var response = await http.Json(request, token);
                    EnsureCurrent(expected, token); return response;
                }
                catch (GoogleApiException e) when (e.Status == HttpStatusCode.Unauthorized && attempt == 0)
                { EnsureCurrent(expected, token); if (accessToken == access) accessToken = ""; }
            }
        }
        internal async Task<List<GoogleCalendarEntry>> ListCalendars(CancellationToken token)
        {
            if (!Connected) return new List<GoogleCalendarEntry>();
            int expected = generation;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                linked.CancelAfter(TimeSpan.FromSeconds(90));
                var result = new List<GoogleCalendarEntry>(); string page = "";
                for (int index = 0; index < 100; index++)
                {
                    var data = await Get("users/me/calendarList?maxResults=250&showHidden=true&minAccessRole=reader&fields="
                        + Uri.EscapeDataString("items(id,summary,summaryOverride),nextPageToken") + "&pageToken=" + Uri.EscapeDataString(page), expected, linked.Token);
                    foreach (var row in GoogleCalendarData.Items(data))
                    {
                        string id = GoogleCalendarData.Text(row, "id");
                        if (id.Length == 0) continue;
                        string name = GoogleCalendarData.Text(row, "summaryOverride");
                        if (name.Length == 0) name = GoogleCalendarData.Text(row, "summary");
                        result.Add(new GoogleCalendarEntry { Id = id, Name = GoogleCalendarData.Clean(name.Length == 0 ? "이름 없는 캘린더" : name),
                            IsSelected = state.Calendars.Any(c => c.Id == id) });
                    }
                    page = GoogleCalendarData.Text(data, "nextPageToken");
                    if (page.Length == 0) return result.GroupBy(c => c.Id).Select(g => g.First()).OrderBy(c => c.Name).ToList();
                }
                throw new InvalidOperationException("캘린더가 너무 많아 목록을 완료하지 못했습니다.");
            }
        }
        internal async Task<GoogleEventResult> Events(DateTime from, DateTime until, bool force, CancellationToken token)
        {
            var result = new GoogleEventResult();
            if (!Enabled) { result.Status = "Google 캘린더 연결·선택이 필요합니다."; return result; }
            if (until <= from || (until - from).TotalDays > 50 || from.Year < 2 || until.Year > 9998)
            { result.Status = "이 날짜 범위는 Google 연동에서 조회할 수 없습니다."; return result; }
            int expected = generation;
            var selected = state.Calendars.ToArray();
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                linked.CancelAfter(TimeSpan.FromSeconds(90));
                foreach (var calendar in selected)
                {
                    EnsureCurrent(expected, linked.Token);
                    string key = calendar.Id + "\n" + from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + until.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    CacheEntry saved; cache.TryGetValue(key, out saved);
                    List<GoogleCalendarEvent> entries;
                    try
                    {
                        if (!force && saved != null && DateTime.UtcNow - saved.Updated < TimeSpan.FromMinutes(5)) entries = saved.Events;
                        else
                        {
                            entries = await FetchCalendar(calendar, from, until, expected, linked.Token);
                            cache[key] = new CacheEntry { Updated = DateTime.UtcNow, Events = entries };
                            while (cache.Count > 64 || cache.Sum(c => c.Value.Events.Count) > 20000)
                                cache.Remove(cache.OrderBy(c => c.Value.Updated).First().Key);
                        }
                    }
                    catch (Exception e) when (!(e is OperationCanceledException) && !linked.IsCancellationRequested)
                    {
                        EnsureCurrent(expected, linked.Token);
                        result.Warnings.Add(calendar.Name + " · 조회 실패" + (saved == null ? " (일정 없음으로 판단하지 마세요)" : " (이전 조회 결과 표시)") + "\n" + ErrorMessage(e));
                        entries = saved == null ? new List<GoogleCalendarEvent>() : saved.Events;
                    }
                    result.Events.AddRange(entries.Select(e => e.Copy()));
                }
            }
            result.Events.Sort((a, b) => { int order = a.Start.CompareTo(b.Start); return order != 0 ? order : string.Compare(a.Title, b.Title, StringComparison.CurrentCulture); });
            result.Status = result.Warnings.Count == 0 ? "Google 캘린더 " + selected.Length + "개 · 최대 5분 캐시 · 새로고침으로 즉시 조회"
                : string.Join("\n", result.Warnings);
            return result;
        }
        private async Task<List<GoogleCalendarEvent>> FetchCalendar(GoogleCalendarEntry calendar, DateTime from, DateTime until, int expected, CancellationToken token)
        {
            var events = new List<GoogleCalendarEvent>(); string page = "";
            // Widen for calendar-specific all-day time zones, then filter by local display dates.
            var query = new Dictionary<string, string> { { "singleEvents", "true" }, { "showDeleted", "false" }, { "orderBy", "startTime" },
                { "maxResults", "2500" }, { "timeZone", "UTC" }, { "fields", "items(id,summary,status,start,end),nextPageToken" },
                { "timeMin", DateTime.SpecifyKind(from.AddDays(-2), DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) },
                { "timeMax", DateTime.SpecifyKind(until.AddDays(2), DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) } };
            for (int index = 0; index < 4; index++)
            {
                query["pageToken"] = page;
                var data = await Get("calendars/" + Uri.EscapeDataString(calendar.Id) + "/events?" + GoogleOAuth.Query(query), expected, token);
                foreach (var row in GoogleCalendarData.Items(data))
                {
                    var item = GoogleCalendarData.Event(row, calendar, TimeZoneInfo.Local);
                    if (item != null && item.Start < until && (item.End > from || item.Start >= from)) events.Add(item);
                }
                page = GoogleCalendarData.Text(data, "nextPageToken");
                if (page.Length == 0) return events.GroupBy(e => e.Key).Select(g => g.First()).ToList();
            }
            throw new InvalidOperationException("조회 범위의 일정이 10,000개를 초과했습니다. 이 캘린더는 선택을 해제하거나 날짜별로 조회하세요.");
        }
        internal static string ErrorMessage(Exception e)
        {
            if (e is GoogleApiException || e is InvalidOperationException || e is System.IO.InvalidDataException) return e.Message;
            if (e is OperationCanceledException) return "조회가 취소되었거나 대기 시간이 초과되었습니다.";
            return "요청을 완료하지 못했습니다. 인터넷 연결과 로컬 파일 권한을 확인한 후 다시 시도하세요.";
        }
        public void Dispose() { lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
    }
}
