using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class GroqPatientVolumeInsightService : IPatientVolumeInsightService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GroqPatientVolumeInsightService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Groq:ApiKey"] ?? "gsk_dummy";
            _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<PatientVolumeInsightNarrative> GenerateInsightsAsync(PatientVolumeInsightContext context)
        {
            var t = context.Trend;
            if (_apiKey == "gsk_dummy") return FallbackNarrative(context);

            var surging = t.SpecialtyTrends.Where(s => s.IsSurging).ToList();
            var busiestDay = t.ProjectedNext7Days.OrderByDescending(d => d.TotalAppointments).FirstOrDefault();
            var overloadedDoctors = context.DoctorLoadForecast.Where(d => d.IsOverloaded).ToList();

            var prompt =
                "You are a hospital operations analyst. You are given ALREADY-COMPUTED patient volume " +
                "numbers -- do not invent, recompute, or contradict any figure below. Your job is only " +
                "to explain them in plain language for a hospital administrator planning staffing.\n\n" +
                $"7-day average daily appointments: {t.Avg7DayAppointments}\n" +
                $"30-day average daily appointments: {t.Avg30DayAppointments}\n" +
                $"7-day average daily unique patients: {t.Avg7DayUniquePatients}\n" +
                $"30-day average daily unique patients: {t.Avg30DayUniquePatients}\n" +
                $"Month-over-month appointment volume change: {t.MonthOverMonthAppointmentChangePercent}%\n" +
                $"Month-over-month unique patient change: {t.MonthOverMonthUniquePatientChangePercent}%\n" +
                $"Predicted next-7-day appointments: {t.PredictedNext7DayAppointments}\n" +
                $"Predicted next-7-day unique patients: {t.PredictedNext7DayUniquePatients}\n" +
                $"Busiest predicted day next week: {(busiestDay == null ? "none" : $"{busiestDay.Date:dddd, MMM d} with {busiestDay.TotalAppointments} appointments")}\n" +
                $"Specialty trends (month-over-month % change): {string.Join(", ", t.SpecialtyTrends.Select(s => $"{s.SpecialtyName}: {s.ChangePercent}%"))}\n" +
                $"Specialties flagged as surging 20%+ (may need more staffing): {(surging.Count == 0 ? "none" : string.Join(", ", surging.Select(s => s.SpecialtyName)))}\n" +
                $"Doctors whose predicted next week is 25%+ above their own typical week (may be overloaded): {(overloadedDoctors.Count == 0 ? "none" : string.Join(", ", overloadedDoctors.Select(d => d.DoctorName)))}\n" +
                $"Anomalies this week vs. the historical baseline: {(context.Anomalies.Count == 0 ? "none" : string.Join("; ", context.Anomalies.Select(a => $"{a.MetricName} is {a.Direction} at {a.RecentValue} vs. a typical {a.BaselineMean} ({a.ZScore} std deviations)")))}\n\n" +
                "Respond with JSON only, no markdown fences, in exactly this shape:\n" +
                "{\"outlook\": \"one sentence summarizing the expected patient load next week\", " +
                "\"insights\": [\"3 to 6 short, specific, actionable sentences -- prioritize calling out any surging specialty or overloaded doctor as needing more staffing attention, the busiest predicted day as one to prepare for, and any anomaly. " +
                "For an anomaly, you were NOT told the actual cause -- suggest 1-2 plausible, general reasons an administrator should check (e.g. a holiday, a reminder-system issue, a doctor's leave), phrased clearly as possibilities to investigate, never as a confirmed cause\"]}";

            var requestBody = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.3,
                response_format = new { type = "json_object" }
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(responseString);
                var raw = jsonDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(raw)) return FallbackNarrative(context);

                raw = StripMarkdownFence(raw);
                var parsed = JsonSerializer.Deserialize<GroqInsightPayload>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.Outlook)) return FallbackNarrative(context);

                return new PatientVolumeInsightNarrative(parsed.Outlook, parsed.Insights ?? new List<string>());
            }
            catch (Exception)
            {
                // Never let a Groq/network hiccup break the dashboard -- fall back to a
                // deterministic, code-generated narrative built from the same trend numbers.
                return FallbackNarrative(context);
            }
        }

        private static string StripMarkdownFence(string raw)
        {
            raw = raw.Trim();
            if (raw.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(7);
            else if (raw.StartsWith("```")) raw = raw.Substring(3);
            if (raw.EndsWith("```")) raw = raw.Substring(0, raw.Length - 3);
            return raw.Trim();
        }

        /// <summary>Used both for the no-API-key dummy mode and as a resilience fallback on any Groq
        /// failure. Deliberately states only the numbers -- no speculative "possible reasons" for
        /// anomalies here, since that reasoning is Groq's job specifically; a fallback that can't
        /// reason just reports what was measured.</summary>
        private static PatientVolumeInsightNarrative FallbackNarrative(PatientVolumeInsightContext context)
        {
            var t = context.Trend;
            var insights = new List<string>();
            var direction = t.MonthOverMonthAppointmentChangePercent >= 0 ? "up" : "down";
            insights.Add($"Appointment volume is {direction} {Math.Abs(t.MonthOverMonthAppointmentChangePercent)}% month-over-month.");

            foreach (var surge in t.SpecialtyTrends.Where(s => s.IsSurging).Take(3))
                insights.Add($"{surge.SpecialtyName} visits are up {surge.ChangePercent}% over the last 30 days -- may need extra staffing.");

            foreach (var overloaded in context.DoctorLoadForecast.Where(d => d.IsOverloaded).Take(3))
                insights.Add($"{overloaded.DoctorName}'s predicted next week ({overloaded.PredictedNext7DayAppointments:0} appointments) is well above their typical week -- may need support.");

            var busiestDay = t.ProjectedNext7Days.OrderByDescending(d => d.TotalAppointments).FirstOrDefault();
            if (busiestDay != null)
                insights.Add($"{busiestDay.Date:dddd, MMM d} is projected to be the busiest day next week with {busiestDay.TotalAppointments} appointments.");

            foreach (var anomaly in context.Anomalies.Take(3))
                insights.Add($"{anomaly.MetricName} is {anomaly.Direction.ToLowerInvariant()} this week at {anomaly.RecentValue} vs. a typical {anomaly.BaselineMean}.");

            var outlook = $"Based on the last 90 days, patient volume is trending {direction} and is projected at roughly {t.PredictedNext7DayAppointments:0} appointments over the next 7 days if current patterns hold.";
            return new PatientVolumeInsightNarrative(outlook, insights);
        }

        private class GroqInsightPayload
        {
            public string? Outlook { get; set; }
            public List<string>? Insights { get; set; }
        }
    }
}
