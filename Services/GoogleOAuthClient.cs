using System;
using System.IO;

namespace DDay3.Services
{
    internal static class GoogleOAuthClient
    {
        internal const string ResourceName = "DDay3.GoogleOAuthClient.json";
        internal static bool IsConfigured
        {
            get { return typeof(GoogleOAuthClient).Assembly.GetManifestResourceInfo(ResourceName) != null; }
        }
        internal static GoogleConnection ReadOfficial()
        {
            using (var stream = typeof(GoogleOAuthClient).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidOperationException(
                    "이 빌드에는 공식 Google 연결 정보가 없습니다. MathTime 공식 배포본을 사용하거나 고급 연결에서 본인의 데스크톱 OAuth JSON을 선택하세요.");
                using (var reader = new StreamReader(stream)) return GoogleCalendarStore.ParseClient(reader.ReadToEnd());
            }
        }
    }
}
