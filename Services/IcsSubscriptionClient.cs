using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DDay3.Services
{
    internal interface IIcsSubscriptionClient : IDisposable
    {
        Task<string> Download(Uri address, CancellationToken token);
    }

    internal sealed class IcsSubscriptionClient : IIcsSubscriptionClient
    {
        private readonly HttpClient http;
        internal IcsSubscriptionClient(HttpMessageHandler handler = null)
        {
            http = new HttpClient(handler ?? new HttpClientHandler
            {
                AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }) { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DDay3/3.5.0");
        }

        internal static Uri ValidateAddress(string value)
        {
            value = (value ?? "").Trim();
            if (value.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) value = "https://" + value.Substring(9);
            Uri address;
            if (value.Length > 4096 || !Uri.TryCreate(value, UriKind.Absolute, out address)
                || address.Scheme != Uri.UriSchemeHttps || address.UserInfo.Length != 0 || address.Fragment.Length != 0
                || address.HostNameType != UriHostNameType.Dns || address.IsLoopback
                || address.Host.IndexOf('.') < 0 || address.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                || address.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("인터넷 캘린더의 HTTPS iCal 주소를 입력하세요. 브라우저용 페이지 주소는 사용할 수 없습니다.");
            return address;
        }

        public async Task<string> Download(Uri address, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                try
                {
                    address = ValidateAddress(address.AbsoluteUri);
                    for (int redirects = 0; redirects <= 3; redirects++)
                    using (var request = new HttpRequestMessage(HttpMethod.Get, address))
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    using (timeout.Token.Register(() => response.Dispose()))
                    {
                        int status = (int)response.StatusCode;
                        if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                        {
                            var location = response.Headers.Location;
                            if (redirects == 3 || location == null) throw new InvalidDataException("캘린더 주소의 이동 횟수가 너무 많거나 이동 주소가 없습니다.");
                            address = ValidateAddress(new Uri(address, location).AbsoluteUri);
                            continue;
                        }
                        if (status == 401 || status == 403) throw new InvalidDataException("캘린더에 접근할 수 없습니다. iCal 주소와 공개 범위를 확인하세요.");
                        if (status == 404 || status == 410) throw new InvalidDataException("캘린더 주소가 없거나 만료되었습니다. 새 iCal 주소를 등록하세요.");
                        if (!response.IsSuccessStatusCode) throw new InvalidDataException("캘린더 서버가 응답하지 않았습니다. 잠시 후 다시 갱신하세요.");
                        if (response.Content == null || response.Content.Headers.ContentLength > IcsCalendarData.MaximumFileBytes)
                            throw new InvalidDataException("캘린더는 5MB까지 가져올 수 있습니다.");
                        string media = response.Content.Headers.ContentType?.MediaType ?? "";
                        if (media.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("웹페이지 주소입니다. iCal 형식의 주소를 복사하세요.");
                        using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var data = new MemoryStream())
                        {
                            var buffer = new byte[8192]; int count;
                            while ((count = await source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) != 0)
                            {
                                if (data.Length + count > IcsCalendarData.MaximumFileBytes)
                                    throw new InvalidDataException("캘린더는 5MB까지 가져올 수 있습니다.");
                                data.Write(buffer, 0, count);
                            }
                            timeout.Token.ThrowIfCancellationRequested(); data.Position = 0;
                            using (var reader = new StreamReader(data, new UTF8Encoding(false, true), true)) return reader.ReadToEnd();
                        }
                    }
                    throw new InvalidDataException("캘린더를 읽을 수 없습니다.");
                }
                catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
                catch (Exception) when (timeout.IsCancellationRequested) { throw new InvalidDataException("캘린더 연결 시간이 초과되었습니다. 다시 갱신하세요."); }
                catch (InvalidDataException) { throw; }
                // Never propagate URI-bearing transport exceptions into UI, logs or the encrypted store.
                catch (Exception) { throw new InvalidDataException("캘린더를 내려받지 못했습니다. 인터넷 연결과 iCal 주소를 확인하세요."); }
            }
        }
        public void Dispose() { http.Dispose(); }
    }
}
