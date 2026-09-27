using System.Globalization;
using System.Text.RegularExpressions;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Parses the attendance-log body a ZKTeco terminal POSTs to /iclock/cdata?table=ATTLOG: one scan
    /// per line, normally tab-separated as <c>PIN, time, status, verify, workcode, ...</c>
    /// (e.g. <c>12\t2026-08-10 09:05:10\t0\t1\t0</c>).
    ///
    /// Written from the published push-protocol shape and deliberately TOLERANT: extra trailing columns,
    /// CRLF or LF, blank lines, a few date formats, and whitespace- instead of tab-separated lines are all
    /// accepted, and a line that can't be read is skipped and counted rather than failing the batch (a
    /// device that gets an error keeps re-sending the same batch forever). It has not been checked against
    /// a physical K40 Pro yet; the raw line is stored with every scan so a mismatch can be corrected from
    /// real traffic.
    /// </summary>
    public static class ZktecoAttlogParser
    {
        private static readonly string[] TimeFormats =
        {
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy/MM/dd HH:mm:ss", "yyyy-M-d H:mm:ss", "dd-MM-yyyy HH:mm:ss",
        };

        // Fallback for space-separated lines: "12 2026-08-10 09:05:10 0 1".
        private static readonly Regex SpaceSeparated = new(
            @"^(?<pin>\S+)\s+(?<time>\d{4}[-/]\d{1,2}[-/]\d{1,2}[ T]\d{1,2}:\d{2}:\d{2})(?:\s+(?<state>-?\d+))?(?:\s+(?<verify>-?\d+))?",
            RegexOptions.Compiled);

        public sealed record ParseResult(IReadOnlyList<IncomingPunch> Punches, int Skipped);

        public static ParseResult Parse(string? body)
        {
            var punches = new List<IncomingPunch>();
            var skipped = 0;
            if (string.IsNullOrWhiteSpace(body)) return new ParseResult(punches, 0);

            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.Trim('\r', '\n', '\0', ' ');
                if (line.Length == 0) continue;

                if (TryParseLine(line, out var punch)) punches.Add(punch);
                else skipped++;
            }

            return new ParseResult(punches, skipped);
        }

        private static bool TryParseLine(string line, out IncomingPunch punch)
        {
            punch = null!;
            string pin, timeText;
            int? state = null, verify = null;

            if (line.Contains('\t'))
            {
                var f = line.Split('\t');
                if (f.Length < 2) return false;
                pin = f[0].Trim();
                timeText = f[1].Trim();
                state = TryInt(f.ElementAtOrDefault(2));
                verify = TryInt(f.ElementAtOrDefault(3));
            }
            else
            {
                var m = SpaceSeparated.Match(line);
                if (!m.Success) return false;
                pin = m.Groups["pin"].Value;
                timeText = m.Groups["time"].Value;
                state = TryInt(m.Groups["state"].Success ? m.Groups["state"].Value : null);
                verify = TryInt(m.Groups["verify"].Success ? m.Groups["verify"].Value : null);
            }

            if (pin.Length == 0) return false;
            if (!DateTime.TryParseExact(timeText, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;

            punch = new IncomingPunch(pin, time, state, verify, line.Length > 500 ? line[..500] : line);
            return true;
        }

        private static int? TryInt(string? value) =>
            int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
