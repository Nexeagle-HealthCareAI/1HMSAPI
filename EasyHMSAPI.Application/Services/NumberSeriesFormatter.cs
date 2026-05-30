using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services
{
    [ExcludeFromCodeCoverage]
    public static class NumberSeriesFormatter
    {
        public static string Format(string? prefix, string? yearFormat, string? separator, int padLength, long value, DateTime? nowUtc = null)
        {
            var sep = separator ?? "-";
            var pad = padLength > 0 ? padLength : 1;
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                parts.Add(prefix!);
            }

            var year = RenderYear(yearFormat, nowUtc ?? DateTime.UtcNow);
            if (!string.IsNullOrEmpty(year))
            {
                parts.Add(year);
            }

            parts.Add(value.ToString().PadLeft(pad, '0'));

            return string.Join(sep, parts);
        }

        private static string RenderYear(string? yearFormat, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(yearFormat)) return string.Empty;

            return yearFormat.Trim().ToUpperInvariant() switch
            {
                "YYYY" => nowUtc.Year.ToString("D4"),
                "YY" => (nowUtc.Year % 100).ToString("D2"),
                _ => string.Empty
            };
        }
    }
}
