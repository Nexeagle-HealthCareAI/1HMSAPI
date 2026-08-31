using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Wires the SOFA/APACHE II auto-fill handlers to the patient's most recently GENERATED
    /// PathologyReport -- both handlers were pure-vitals draft composers with lab fields
    /// permanently null ("no structured lab-results system to pull them from") until the 1Lab
    /// Suite's result pipeline (EnterPathologyResultHandler) existed to pull from. Auto-fill is a
    /// convenience only: the resolved values just pre-populate the same freely-editable form
    /// inputs the clinician already reviews and can overwrite before submitting the score. Used to
    /// key off report.Status == "APPROVED" back when reports went through a technician/pathologist
    /// sign-off pipeline; that pipeline was removed (a report is just generated, no approval gate),
    /// so this now takes whichever report is most recently generated instead.
    /// </summary>
    public static class PathologyLabValueResolver
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Parameter name -> numeric value, from every result line on the patient's most
        /// recently generated report, plus that report's generation timestamp. Case-insensitive
        /// keys since the exact catalog spelling isn't guaranteed stable across hospitals.
        /// Non-numeric or unparseable results are skipped.</summary>
        public static async Task<(Dictionary<string, decimal> Values, DateTime? ApprovedAt)> GetLatestApprovedValuesAsync(
            AppDbContext context, Guid hospitalId, string? patientId, CancellationToken cancellationToken)
        {
            var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(patientId)) return (values, null);

            var latestReport = await (
                from report in context.PathologyReport
                join order in context.PathologyOrder on report.OrderId equals order.OrderId
                where order.HospitalId == hospitalId && order.PatientId == patientId
                orderby report.GeneratedAt descending
                select report
            ).FirstOrDefaultAsync(cancellationToken);

            if (latestReport == null) return (values, null);

            var results = await context.PathologyResult
                .Where(r => r.ReportId == latestReport.ReportId)
                .Select(r => r.ResultValuesJson)
                .ToListAsync(cancellationToken);

            foreach (var resultJson in results)
            {
                Dictionary<string, JsonElement>? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultJson, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (parsed == null) continue;

                foreach (var (paramName, entry) in parsed)
                {
                    // Handles both the enriched {value, flag} shape and the older raw-string shape.
                    string? rawValue = entry.ValueKind switch
                    {
                        JsonValueKind.Object when entry.TryGetProperty("value", out var v) => v.GetString(),
                        JsonValueKind.String => entry.GetString(),
                        _ => null,
                    };
                    if (rawValue != null && decimal.TryParse(rawValue, out var numeric))
                    {
                        values[paramName] = numeric;
                    }
                }
            }

            return (values, latestReport.GeneratedAt);
        }

        public static decimal? TryGet(Dictionary<string, decimal> values, string paramName) =>
            values.TryGetValue(paramName, out var v) ? v : null;
    }
}
