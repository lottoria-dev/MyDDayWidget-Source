using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DDay3.Models;

namespace DDay3.Services
{
    // Entire connection record is protected for the current Windows user. No credentials in INI or logs.
    internal sealed class GoogleConnection
    {
        public GoogleConnection() { }
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public List<GoogleCalendarEntry> Calendars { get; set; } = new List<GoogleCalendarEntry>();
    }
    internal sealed class GoogleCalendarStore
    {
        private readonly string path;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DDay3.GoogleCalendar.1");
        internal GoogleCalendarStore(string directory) { path = Path.Combine(directory, "google-calendar.bin"); }
        internal GoogleConnection Read()
        {
            if (!File.Exists(path)) return new GoogleConnection();
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException();
            byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var state = GoogleCalendarData.Serializer().Deserialize<GoogleConnection>(Encoding.UTF8.GetString(clear));
                if (state == null || state.Calendars == null || state.Calendars.Count > 20) throw new InvalidDataException();
                return state;
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        internal void Save(GoogleConnection state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] clear = Encoding.UTF8.GetBytes(GoogleCalendarData.Serializer().Serialize(state));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { Array.Clear(clear, 0, clear.Length); if (File.Exists(temp)) File.Delete(temp); }
        }
        internal void Delete() { if (File.Exists(path)) File.Delete(path); }
        internal static GoogleConnection ReadClient(string path)
        {
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("OAuth 파일이 너무 큽니다.");
            return ParseClient(File.ReadAllText(path));
        }
        internal static GoogleConnection ParseClient(string json)
        {
            if (json == null || json.Length > 65536) throw new InvalidDataException("OAuth 파일이 너무 큽니다.");
            var installed = GoogleCalendarData.Child(GoogleCalendarData.Object(json), "installed");
            string id = GoogleCalendarData.Text(installed, "client_id");
            if (installed == null || !Regex.IsMatch(id, @"\A[A-Za-z0-9._-]+\.apps\.googleusercontent\.com\z"))
                throw new InvalidDataException("Google Cloud에서 데스크톱 앱 유형으로 만든 OAuth JSON을 선택하세요.");
            return new GoogleConnection { ClientId = id, ClientSecret = GoogleCalendarData.Text(installed, "client_secret") };
        }
    }
}
