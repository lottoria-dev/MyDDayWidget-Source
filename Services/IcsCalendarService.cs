using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DDay3.Models;

namespace DDay3.Services
{
    internal sealed class IcsCalendarService : IDisposable
    {
        private const int MaximumStoreBytes = 20 * 1024 * 1024;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DDay3.IcsTitles.1");
        private readonly string path;
        private readonly object state = new object();
        private readonly SemaphoreSlim evaluation = new SemaphoreSlim(1), refresh = new SemaphoreSlim(1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly IIcsSubscriptionClient client;
        private List<IcsCalendarSnapshot> calendars = new List<IcsCalendarSnapshot>();
        private readonly Dictionary<string, List<CalendarEvent>> cache = new Dictionary<string, List<CalendarEvent>>();
        private int revision;
        private bool disposed;
        internal event EventHandler Changed;
        internal string LoadMessage { get; private set; } = "";
        internal bool Enabled { get { lock (state) return calendars.Any(c => c.Enabled); } }
        internal IcsCalendarService(string directory, IIcsSubscriptionClient client = null)
        {
            this.client = client ?? new IcsSubscriptionClient();
            path = Path.Combine(directory, "ics-calendars.bin");
            try { calendars = Read(); }
            catch (Exception) { LoadMessage = "저장된 캘린더를 읽지 못했습니다. 파일이나 URL을 다시 등록해 주세요."; }
        }
        internal List<IcsCalendarSnapshot> Calendars { get { lock (state) return calendars.Select(c => c.Copy()).ToList(); } }

        internal static Task<List<IcsCalendarSnapshot>> ReadFiles(string[] paths, CancellationToken token)
        {
            if (paths == null || paths.Length == 0 || paths.Length > 20) throw new InvalidDataException("파일은 한 번에 20개까지 선택할 수 있습니다.");
            return Task.Run(() =>
            {
                var result = new List<IcsCalendarSnapshot>();
                foreach (string file in paths) { token.ThrowIfCancellationRequested(); result.Add(IcsCalendarData.ReadFile(file)); }
                return result;
            }, token);
        }
        // Only merge editable display choices; a background refresh must not be overwritten by stale UI snapshots.
        private List<IcsCalendarSnapshot> WithSelection(IEnumerable<IcsCalendarSnapshot> selection)
        {
            var next = calendars.Select(c => c.Copy()).ToList();
            if (selection != null) foreach (var value in selection)
            {
                var current = next.FirstOrDefault(c => c.Id == value.Id);
                if (current == null) continue;
                current.Enabled = value.Enabled;
                if (current.IsSubscription) current.RefreshMinutes = CheckInterval(value.RefreshMinutes);
            }
            return next;
        }
        internal static int CheckInterval(int minutes)
        {
            if (minutes != 0 && (minutes < 15 || minutes > 1440)) throw new InvalidDataException("자동 갱신은 15~1440분 또는 수동으로 지정하세요.");
            return minutes;
        }
        internal void SaveSelection(IEnumerable<IcsCalendarSnapshot> selection)
        { lock (state) Write(WithSelection(selection)); Changed?.Invoke(this, EventArgs.Empty); }
        internal void Remove(string id, IEnumerable<IcsCalendarSnapshot> selection = null)
        { lock (state) Write(WithSelection(selection).Where(c => c.Id != id).ToList()); Changed?.Invoke(this, EventArgs.Empty); }

        internal void Import(IEnumerable<IcsCalendarSnapshot> imported, string replaceId = null, IEnumerable<IcsCalendarSnapshot> selection = null)
        {
            lock (state)
            {
                var next = WithSelection(selection);
                var incoming = imported.Select(c => c.Copy()).ToList();
                if (incoming.Any(c => c.IsSubscription)) throw new InvalidDataException("URL은 구독 등록으로 추가하세요.");
                if (replaceId != null)
                {
                    if (incoming.Count != 1) throw new InvalidDataException("갱신할 파일 하나를 선택하세요.");
                    int index = next.FindIndex(c => c.Id == replaceId && !c.IsSubscription);
                    if (index < 0) throw new InvalidDataException("갱신할 파일을 찾을 수 없습니다.");
                    incoming[0].Id = next[index].Id; incoming[0].Enabled = next[index].Enabled;
                    next[index] = incoming[0];
                }
                else foreach (var value in incoming)
                {
                    var existing = next.Where(c => !c.IsSubscription && string.Equals(c.Name, value.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (existing.Length == 1)
                    { value.Id = existing[0].Id; value.Enabled = existing[0].Enabled; next[next.IndexOf(existing[0])] = value; }
                    else if (!next.Any(c => !c.IsSubscription && c.Content == value.Content)) next.Add(value);
                }
                Write(next);
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
        internal async Task Subscribe(string url, string name, int minutes, CancellationToken token)
        {
            var address = IcsSubscriptionClient.ValidateAddress(url); CheckInterval(minutes);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                await refresh.WaitAsync(linked.Token);
                try
                {
                    var value = await Download(address, IcsCalendarData.CleanTitle(name), linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    lock (state)
                    {
                        var next = calendars.Select(c => c.Copy()).ToList();
                        var existing = next.FirstOrDefault(c => c.SubscriptionUrl == address.AbsoluteUri);
                        if (existing != null) { value.Id = existing.Id; value.Enabled = existing.Enabled; next.Remove(existing); }
                        if (!string.IsNullOrWhiteSpace(name)) value.Name = IcsCalendarData.CleanTitle(name);
                        value.SubscriptionUrl = address.AbsoluteUri; value.RefreshMinutes = minutes; value.LastAttemptUtc = DateTime.UtcNow;
                        if (existing == null) next.Add(value); else next.Insert(calendars.FindIndex(c => c.Id == existing.Id), value);
                        Write(next);
                    }
                    Changed?.Invoke(this, EventArgs.Empty);
                }
                finally { refresh.Release(); }
            }
        }
        private async Task<IcsCalendarSnapshot> Download(Uri address, string name, CancellationToken token)
        {
            string text;
            try { text = await client.Download(address, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw new InvalidDataException("캘린더를 내려받지 못했습니다. 인터넷 연결과 iCal 주소를 확인하세요."); }
            token.ThrowIfCancellationRequested();
            try
            {
                return await Task.Run(() => IcsCalendarData.Parse(text, string.IsNullOrWhiteSpace(name) ? "구독 캘린더" : name), token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw new InvalidDataException("iCal 일정을 읽을 수 없습니다. 주소와 일정 파일 형식을 확인하세요."); }
        }
        internal static bool IsDue(IcsCalendarSnapshot value, DateTime now)
        {
            return value.IsSubscription && value.Enabled && value.RefreshMinutes > 0
                && (value.LastAttemptUtc > now.AddMinutes(5) || now - value.LastAttemptUtc >= TimeSpan.FromMinutes(value.RefreshMinutes));
        }
        internal async Task RefreshDue(CancellationToken token)
        {
            foreach (var source in Calendars.Where(c => IsDue(c, DateTime.UtcNow)))
            {
                token.ThrowIfCancellationRequested();
                try { await RefreshSubscription(source.Id, token, true); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { /* A store error on one feed must not block the others. */ }
            }
        }
        // Returns a safe status; transport exception details can contain secret URLs and must never escape.
        internal async Task<string> RefreshSubscription(string id, CancellationToken token, bool onlyIfDue = false)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                await refresh.WaitAsync(linked.Token);
                try
                {
                    var source = Calendars.FirstOrDefault(c => c.Id == id && c.IsSubscription);
                    if (source == null || (onlyIfDue && !IsDue(source, DateTime.UtcNow))) return "";
                    lock (state)
                    {
                        var current = calendars.FirstOrDefault(c => c.Id == id && c.SubscriptionUrl == source.SubscriptionUrl);
                        if (current == null) return "";
                        current.LastAttemptUtc = DateTime.UtcNow;
                    }
                    IcsCalendarSnapshot incoming = null; string error = "";
                    try { incoming = await Download(IcsSubscriptionClient.ValidateAddress(source.SubscriptionUrl), source.Name, linked.Token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception) { error = "갱신하지 못했습니다. 이전 일정을 유지합니다. 주소 또는 인터넷 연결을 확인하세요."; }
                    linked.Token.ThrowIfCancellationRequested();
                    lock (state)
                    {
                        var next = calendars.Select(c => c.Copy()).ToList();
                        int index = next.FindIndex(c => c.Id == id && c.SubscriptionUrl == source.SubscriptionUrl);
                        if (index < 0) return ""; // Removed while downloading: never resurrect it.
                        var current = next[index];
                        current.LastAttemptUtc = DateTime.UtcNow; current.RefreshError = error;
                        if (incoming != null)
                        { current.Content = incoming.Content; current.EventCount = incoming.EventCount; current.ImportedUtc = incoming.ImportedUtc; }
                        Write(next);
                    }
                    Changed?.Invoke(this, EventArgs.Empty);
                    return error;
                }
                finally { refresh.Release(); }
            }
        }
        internal void Save(IList<IcsCalendarSnapshot> values)
        { lock (state) Write(values); Changed?.Invoke(this, EventArgs.Empty); }
        private void Write(IList<IcsCalendarSnapshot> values)
        {
            if (disposed) throw new OperationCanceledException();
            if (values.Count > 20 || values.Select(c => c.Id).Distinct().Count() != values.Count) throw new InvalidDataException("캘린더는 20개까지 저장할 수 있습니다.");
            foreach (var c in values)
            { if (c.IsSubscription) { IcsSubscriptionClient.ValidateAddress(c.SubscriptionUrl); CheckInterval(c.RefreshMinutes); } }
            var xml = new XElement("IcsTitles", new XAttribute("version", 2), values.Select(c =>
                new XElement("Calendar", new XAttribute("id", c.Id), new XAttribute("name", c.Name),
                    new XAttribute("enabled", c.Enabled), new XAttribute("imported", c.ImportedUtc.ToString("o")),
                    new XAttribute("count", c.EventCount), new XAttribute("minutes", c.RefreshMinutes),
                    new XAttribute("attempt", c.LastAttemptUtc.ToString("o")), new XElement("Url", c.SubscriptionUrl),
                    new XElement("Error", c.RefreshError), new XElement("Content", c.Content))));
            byte[] clear = Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
            if (clear.Length > MaximumStoreBytes) { Array.Clear(clear, 0, clear.Length); throw new InvalidDataException("저장할 캘린더의 총 용량이 너무 큽니다."); }
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(temp, ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                calendars = values.Select(c => c.Copy()).ToList(); revision++; cache.Clear(); LoadMessage = "";
            }
            finally { Array.Clear(clear, 0, clear.Length); if (File.Exists(temp)) File.Delete(temp); }
        }
        private List<IcsCalendarSnapshot> Read()
        {
            if (!File.Exists(path)) return new List<IcsCalendarSnapshot>();
            if (new FileInfo(path).Length > MaximumStoreBytes + 65536) throw new InvalidDataException();
            byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                using (var stream = new MemoryStream(clear))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumStoreBytes }))
                {
                    var root = XElement.Load(reader); int version = (int?)root.Attribute("version") ?? 0;
                    if (root.Name != "IcsTitles" || (version != 1 && version != 2) || root.Elements("Calendar").Count() > 20) throw new InvalidDataException();
                    var result = new List<IcsCalendarSnapshot>();
                    foreach (var row in root.Elements("Calendar"))
                    {
                        var value = IcsCalendarData.Parse((string)row.Element("Content"), (string)row.Attribute("name"));
                        Guid id; if (!Guid.TryParseExact((string)row.Attribute("id"), "N", out id)) throw new InvalidDataException();
                        value.Id = id.ToString("N"); value.Name = IcsCalendarData.CleanTitle((string)row.Attribute("name"));
                        value.Enabled = (bool?)row.Attribute("enabled") ?? true;
                        value.ImportedUtc = XmlConvert.ToDateTime((string)row.Attribute("imported"), XmlDateTimeSerializationMode.Utc);
                        value.SubscriptionUrl = (string)row.Element("Url") ?? "";
                        if (value.IsSubscription) value.SubscriptionUrl = IcsSubscriptionClient.ValidateAddress(value.SubscriptionUrl).AbsoluteUri;
                        value.RefreshMinutes = CheckInterval((int?)row.Attribute("minutes") ?? 30);
                        value.LastAttemptUtc = row.Attribute("attempt") == null ? value.ImportedUtc
                            : XmlConvert.ToDateTime((string)row.Attribute("attempt"), XmlDateTimeSerializationMode.Utc);
                        value.RefreshError = string.IsNullOrEmpty((string)row.Element("Error")) ? "" : "갱신하지 못했습니다. 이전 일정을 유지합니다.";
                        result.Add(value);
                    }
                    if (result.Select(c => c.Id).Distinct().Count() != result.Count) throw new InvalidDataException();
                    return result;
                }
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        internal async Task<List<CalendarEvent>> Events(DateTime begin, DateTime end, CancellationToken token)
        {
            if (end <= begin || end - begin > TimeSpan.FromDays(45)) return new List<CalendarEvent>();
            int expected; string key; IcsCalendarSnapshot[] sources;
            lock (state)
            {
                expected = revision; key = expected + ":" + begin.Ticks + ":" + end.Ticks;
                List<CalendarEvent> saved;
                if (cache.TryGetValue(key, out saved)) return saved.Select(e => e.Copy()).ToList();
                sources = calendars.Where(c => c.Enabled).Select(c => c.Copy()).ToArray();
            }
            await evaluation.WaitAsync(token);
            try
            {
                var events = await Task.Run(() =>
                {
                    var found = new List<CalendarEvent>();
                    foreach (var source in sources)
                    {
                        token.ThrowIfCancellationRequested(); found.AddRange(IcsCalendarData.Events(source, begin, end));
                        if (found.Count > 20000) throw new InvalidDataException("표시할 일정이 너무 많습니다.");
                    }
                    return found.GroupBy(e => e.Key).Select(g => g.First()).OrderBy(e => e.Start).ThenBy(e => e.Title).ToList();
                }, token);
                token.ThrowIfCancellationRequested();
                lock (state)
                {
                    if (expected != revision) throw new OperationCanceledException();
                    if (cache.Count >= 16) cache.Clear(); cache[key] = events;
                }
                return events.Select(e => e.Copy()).ToList();
            }
            finally { evaluation.Release(); }
        }
        public void Dispose()
        {
            lock (state) { if (disposed) return; disposed = true; }
            lifetime.Cancel(); client.Dispose();
            // In-flight continuations still release these semaphores and linked cancellation registrations.
        }
    }
}
