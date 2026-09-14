using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Synerixis.Api.Helpers
{
    /// <summary>轻量 CSV 导出（UTF-8 BOM，Excel 友好）。</summary>
    public static class CsvExport
    {
        public static string Escape(object? value)
        {
            if (value == null) return string.Empty;
            var s = value switch
            {
                DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static string Line(params object?[] cells)
            => string.Join(",", cells.Select(Escape));

        public static FileContentResult File(string csvBody, string fileName)
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bytes = utf8.GetBytes(csvBody);
            return new FileContentResult(bytes, "text/csv; charset=utf-8")
            {
                FileDownloadName = fileName
            };
        }
    }
}
