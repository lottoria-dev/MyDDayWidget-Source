using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace DDay3.Services
{
    internal enum SpecialDateKind { Holiday, SolarTerm }

    internal sealed class HolidayEntry
    {
        internal DateTime Date { get; set; }
        internal string Name { get; set; }
    }

    internal static class HolidayData
    {
        internal const string Endpoint = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo";
        internal const string SolarTermEndpoint = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/get24DivisionsInfo";
        internal const string SourcePage = "https://www.data.go.kr/data/15012690/openapi.do";

        internal static string NormalizeKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            // Accept both portal representations without interpreting a raw '+' as a space.
            if (value.Contains("%")) value = Uri.UnescapeDataString(value);
            if (value.Length == 0 || value.Length > 512 || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl))
                throw new ArgumentException("공공데이터포털 인증키를 확인해 주세요.");
            return value;
        }

        internal static Uri RequestUri(int year, string key, SpecialDateKind kind = SpecialDateKind.Holiday, int? month = null)
        {
            if (kind == SpecialDateKind.SolarTerm && (!month.HasValue || month.Value < 1 || month.Value > 12))
                throw new ArgumentOutOfRangeException("month");
            if (year < 1 || year > 9999) throw new ArgumentOutOfRangeException("year");
            return new Uri((kind == SpecialDateKind.SolarTerm ? SolarTermEndpoint : Endpoint) + "?ServiceKey=" + Uri.EscapeDataString(NormalizeKey(key)) +
                "&solYear=" + year.ToString("0000", CultureInfo.InvariantCulture) + "&pageNo=1&numOfRows=1000" +
                (month.HasValue ? "&solMonth=" + month.Value.ToString("00", CultureInfo.InvariantCulture) : ""));
        }

        internal static string SolarTermCache(IEnumerable<HolidayEntry> entries)
        {
            var values = entries.OrderBy(x => x.Date).ToArray();
            if (values.Length != 24 || values.Select(x => x.Name).Distinct().Count() != 24)
                throw new FormatException("24절기 연간 자료가 불완전합니다.");
            return new XDocument(new XElement("response",
                new XElement("header", new XElement("resultCode", "00")),
                new XElement("body", new XElement("totalCount", values.Length),
                    new XElement("items", values.Select(x => new XElement("item",
                        new XElement("locdate", x.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                        new XElement("dateName", x.Name))))))).ToString();
        }

        internal static List<HolidayEntry> Parse(string xml, int year, SpecialDateKind kind = SpecialDateKind.Holiday)
        {
            if (xml == null || xml.Length > 1024 * 1024) throw new FormatException("특일 응답의 크기가 올바르지 않습니다.");
            var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024 };
            XDocument doc;
            using (var reader = XmlReader.Create(new StringReader(xml), options)) doc = XDocument.Load(reader);
            var header = doc.Root == null ? null : doc.Root.Element("header");
            if (doc.Root == null || doc.Root.Name != "response" || header == null || (string)header.Element("resultCode") != "00")
                throw new FormatException("인증키·활용신청 승인·API 상태를 확인해 주세요.");
            var body = doc.Root.Element("body");
            int total;
            if (body == null || !int.TryParse((string)body.Element("totalCount"), out total) || total <= 0 || total > 1000)
                throw new FormatException("해당 연도 특일 자료가 아직 없거나 응답이 불완전합니다.");
            var items = body.Element("items");
            if (items == null || items.Elements("item").Count() != total)
                throw new FormatException("특일 자료 일부만 수신되어 기존 자료를 유지합니다.");
            var result = new List<HolidayEntry>();
            foreach (var item in items.Elements("item"))
            {
                DateTime date;
                string name = ((string)item.Element("dateName") ?? string.Empty).Trim();
                string flag = (string)item.Element("isHoliday");
                if (!DateTime.TryParseExact((string)item.Element("locdate"), "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date) || date.Year != year || name.Length == 0 || name.Length > 160 ||
                    (kind == SpecialDateKind.Holiday && flag != "Y" && flag != "N"))
                    throw new FormatException("특일 날짜 또는 이름이 올바르지 않습니다.");
                // Solar terms are astronomical dates, not public holidays (isHoliday may be absent).
                if ((kind == SpecialDateKind.SolarTerm || flag == "Y") && !result.Any(x => x.Date == date && x.Name == name))
                    result.Add(new HolidayEntry { Date = date, Name = name });
            }
            if (result.Count == 0) throw new FormatException("확인할 수 있는 특일 자료가 없습니다.");
            return result;
        }
    }
}
