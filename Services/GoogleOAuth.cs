using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDay3.Services
{
    internal sealed class GoogleApiException : Exception
    {
        internal HttpStatusCode Status { get; }
        internal GoogleApiException(HttpStatusCode status) : base(status == HttpStatusCode.Unauthorized
            ? "Google 연결이 만료되었습니다. 다시 연결해 주세요."
            : status == HttpStatusCode.Forbidden ? "Calendar API 사용 설정과 캘린더 읽기 권한을 확인하세요."
            : (int)status == 429 ? "Google 조회 한도를 초과했습니다. 잠시 후 다시 시도하세요."
            : "Google 요청을 완료하지 못했습니다. 연결 상태와 계정 권한을 확인하세요.") { Status = status; }
    }
    internal class GoogleHttp : IDisposable
    {
        private readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        internal virtual async Task<Dictionary<string, object>> Json(HttpRequestMessage request, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                try { return await ReadJson(request, timeout.Token); }
                catch (Exception e) when (timeout.IsCancellationRequested && !token.IsCancellationRequested
                    && (e is OperationCanceledException || e is IOException || e is ObjectDisposedException))
                { throw new InvalidOperationException("Google 요청 시간이 초과되었습니다. 잠시 후 새로고침하세요."); }
            }
        }
        private async Task<Dictionary<string, object>> ReadJson(HttpRequestMessage request, CancellationToken token)
        {
            using (request)
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
            {
                if (!response.IsSuccessStatusCode) throw new GoogleApiException(response.StatusCode);
                if (response.Content.Headers.ContentLength > 4 * 1024 * 1024) throw new InvalidDataException("Google 응답이 너무 큽니다.");
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var body = new MemoryStream())
                using (token.Register(() => stream.Dispose()))
                {
                    byte[] buffer = new byte[8192]; int length;
                    while ((length = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        if (body.Length + length > 4 * 1024 * 1024) throw new InvalidDataException("Google 응답이 너무 큽니다.");
                        body.Write(buffer, 0, length);
                    }
                    return GoogleCalendarData.Object(Encoding.UTF8.GetString(body.ToArray()));
                }
            }
        }
        internal virtual Task<Dictionary<string, object>> Token(Dictionary<string, string> values, CancellationToken token)
        {
            return Json(new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
                { Content = new FormUrlEncodedContent(values) }, token);
        }
        internal virtual async Task Revoke(string refreshToken, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke")
                { Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "token", refreshToken } }) })
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                    if (!response.IsSuccessStatusCode) throw new GoogleApiException(response.StatusCode);
            }
        }
        public void Dispose() { client.Dispose(); }
    }

    internal static class GoogleOAuth
    {
        internal const string Scopes = "https://www.googleapis.com/auth/calendar.calendarlist.readonly https://www.googleapis.com/auth/calendar.events.readonly";
        internal static string RandomString()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Base64Url(bytes);
        }
        private static string Base64Url(byte[] value) { return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        internal static string Challenge(string verifier)
        { using (var sha = SHA256.Create()) return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier))); }
        internal static string Query(IEnumerable<KeyValuePair<string, string>> values)
        { return string.Join("&", values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))); }

        internal static async Task<Dictionary<string, object>> Connect(GoogleConnection credentials, GoogleHttp http, CancellationToken cancellation)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(3));
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start(1);
                try
                {
                    string redirect = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture) + "/oauth2/callback";
                    string state = RandomString(), verifier = RandomString();
                    string url = "https://accounts.google.com/o/oauth2/v2/auth?" + Query(new Dictionary<string, string> {
                        { "client_id", credentials.ClientId }, { "redirect_uri", redirect }, { "response_type", "code" },
                        { "scope", Scopes }, { "state", state }, { "code_challenge", Challenge(verifier) },
                        { "code_challenge_method", "S256" }, { "access_type", "offline" }, { "prompt", "consent select_account" } });
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    string code;
                    using (timeout.Token.Register(listener.Stop))
                        code = await ReceiveCode(listener, state, timeout.Token);
                    var values = new Dictionary<string, string> { { "client_id", credentials.ClientId }, { "code", code },
                        { "grant_type", "authorization_code" }, { "redirect_uri", redirect }, { "code_verifier", verifier } };
                    if (!string.IsNullOrEmpty(credentials.ClientSecret)) values.Add("client_secret", credentials.ClientSecret);
                    var token = await http.Token(values, timeout.Token);
                    string scope = GoogleCalendarData.Text(token, "scope");
                    if (scope.Length != 0 && Scopes.Split(' ').Any(s => !scope.Split(' ').Contains(s)))
                        throw new InvalidOperationException("캘린더 목록과 일정 읽기 권한을 모두 허용해야 연결할 수 있습니다.");
                    if (GoogleCalendarData.Text(token, "refresh_token").Length == 0 || GoogleCalendarData.Text(token, "access_token").Length == 0)
                        throw new InvalidOperationException("지속 연결 권한을 받지 못했습니다. Google 권한 화면에서 다시 연결하세요.");
                    cancellation.ThrowIfCancellationRequested();
                    return token;
                }
                catch (SocketException) when (timeout.IsCancellationRequested) { throw new OperationCanceledException(); }
                catch (ObjectDisposedException) when (timeout.IsCancellationRequested) { throw new OperationCanceledException(); }
                finally { listener.Stop(); }
            }
        }
        private static async Task<string> ReceiveCode(TcpListener listener, string state, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                using (TcpClient connection = await listener.AcceptTcpClientAsync())
                using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (readTimeout.Token.Register(connection.Close))
                {
                    readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var stream = connection.GetStream();
                    string request;
                    try
                    {
                        byte[] bytes = new byte[8192]; int count = 0;
                        while (count < bytes.Length)
                        {
                            int n = await stream.ReadAsync(bytes, count, bytes.Length - count, readTimeout.Token);
                            if (n == 0) break;
                            count += n;
                            if (Encoding.ASCII.GetString(bytes, 0, count).Contains("\r\n\r\n")) break;
                        }
                        request = Encoding.ASCII.GetString(bytes, 0, count);
                    }
                    catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is OperationCanceledException)
                    { token.ThrowIfCancellationRequested(); continue; }
                    string[] firstLine = request.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
                    Uri target;
                    if (firstLine.Length != 3 || firstLine[0] != "GET" || !firstLine[1].StartsWith("/oauth2/callback?", StringComparison.Ordinal)
                        || !Uri.TryCreate("http://127.0.0.1" + firstLine[1], UriKind.Absolute, out target)) continue;
                    var values = ParseQuery(target.Query);
                    string receivedState, code, error;
                    if (!values.TryGetValue("state", out receivedState) || receivedState != state) continue;
                    // Do not reflect the authorization code, account, or any provider text into the browser.
                    byte[] body = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><title>D-Day 3</title><p>인증 응답을 받았습니다. D-Day 3 앱으로 돌아가세요.</p>");
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n");
                    try { await stream.WriteAsync(header, 0, header.Length, readTimeout.Token); await stream.WriteAsync(body, 0, body.Length, readTimeout.Token); }
                    catch (IOException) { } // Browser closing must not discard a valid authorization response.
                    catch (ObjectDisposedException) { }
                    if (values.TryGetValue("error", out error)) throw new InvalidOperationException("Google 연결이 승인되지 않았습니다.");
                    if (values.TryGetValue("code", out code) && code.Length > 0) return code;
                    throw new InvalidOperationException("Google 인증 응답을 확인할 수 없습니다.");
                }
            }
        }
        internal static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                string[] pair = part.Split(new[] { '=' }, 2);
                if (pair.Length == 2)
                {
                    string key = Uri.UnescapeDataString(pair[0]);
                    if (result.ContainsKey(key)) return new Dictionary<string, string>();
                    result.Add(key, Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
                }
            }
            return result;
        }
    }
}
