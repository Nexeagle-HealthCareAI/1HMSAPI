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
            var busiestDay = t.ProjectedNext30Days.OrderByDescending(d => d.TotalAppointments).FirstOrDefault();
            var overloadedDoctors = context.DoctorLoadForecast.Where(d => d.IsOverloaded).ToList();
            var notableMonths = t.MonthlySeasonalFactors.Where(m => m.IsNotable).ToList();

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
                $"Predicted tomorrow appointments: {t.PredictedTomorrowAppointments}\n" +
                $"Predicted next-7-day appointments: {t.PredictedNext7DayAppointments}\n" +
                $"Predicted next-30-day appointments: {t.PredictedNext30DayAppointments}\n" +
                $"Predicted next-30-day unique patients: {t.PredictedNext30DayUniquePatients}\n" +
                $"Historical no-show rate (last ~90 days): {Math.Round(context.NoShowRate * 100, 1)}% -- of the predicted appointments above, roughly that share typically don't show up\n" +
                $"Busiest predicted day next month: {(busiestDay == null ? "none" : $"{busiestDay.Date:dddd, MMM d} with {busiestDay.TotalAppointments} appointments")}\n" +
                $"Specialty trends (month-over-month % change): {string.Join(", ", t.SpecialtyTrends.Select(s => $"{s.SpecialtyName}: {s.ChangePercent}%"))}\n" +
                $"Specialties flagged as surging 20%+ (may need more staffing): {(surging.Count == 0 ? "none" : string.Join(", ", surging.Select(s => s.SpecialtyName)))}\n" +
                $"Doctors whose predicted next month is 25%+ above their own typical month (may be overloaded): {(overloadedDoctors.Count == 0 ? "none" : string.Join(", ", overloadedDoctors.Select(d => d.DoctorName)))}\n" +
                $"Anomalies this week vs. the historical baseline: {(context.Anomalies.Count == 0 ? "none" : string.Join("; ", context.Anomalies.Select(a => $"{a.MetricName} is {a.Direction} at {a.RecentValue} vs. a typical {a.BaselineMean} ({a.ZScore} std deviations)")))}\n" +
                $"Calendar months that historically run notably busier/quieter for this hospital (from its full history): {(notableMonths.Count == 0 ? "none" : string.Join(", ", notableMonths.Select(m => $"{m.MonthName}: {(m.Index > 1 ? "+" : "")}{Math.Round((m.Index - 1) * 100, 0)}% vs. average")))}\n\n" +
                "Respond with JSON only, no markdown fences, in exactly this shape:\n" +
                "{\"outlook\": \"one sentence summarizing the expected patient load over the next 30 days\", " +
                "\"insights\": [\"3 to 7 short, specific, actionable sentences -- prioritize calling out any surging specialty or overloaded doctor as needing more staffing attention, the busiest predicted day as one to prepare for, any anomaly, any notable upcoming seasonal month within the next 30 days, and what the no-show rate implies for how many patients will actually attend vs. how many are booked. " +
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
                insights.Add($"{overloaded.DoctorName}'s predicted next 30 days ({overloaded.PredictedNext30DayAppointments:0} appointments) is well above their typical month -- may need support.");

            var busiestDay = t.ProjectedNext30Days.OrderByDescending(d => d.TotalAppointments).FirstOrDefault();
            if (busiestDay != null)
                insights.Add($"{busiestDay.Date:dddd, MMM d} is projected to be the busiest day in the next 30 days with {busiestDay.TotalAppointments} appointments.");

            foreach (var anomaly in context.Anomalies.Take(3))
                insights.Add($"{anomaly.MetricName} is {anomaly.Direction.ToLowerInvariant()} this week at {anomaly.RecentValue} vs. a typical {anomaly.BaselineMean}.");

            var projectedMonths = t.ProjectedNext30Days.Select(d => d.Date.Month).Distinct().ToHashSet();
            foreach (var seasonal in t.MonthlySeasonalFactors.Where(m => m.IsNotable && projectedMonths.Contains(m.Month)).Take(2))
            {
                var seasonalDirection = seasonal.Index > 1 ? "busier" : "quieter";
                insights.Add($"{seasonal.MonthName} has historically run about {Math.Abs(Math.Round((seasonal.Index - 1) * 100, 0))}% {seasonalDirection} than average for this hospital.");
            }

            if (context.NoShowRate > 0m)
            {
                var expectedNext7 = Math.Round(t.PredictedNext7DayAppointments * (1 - context.NoShowRate), 0);
                insights.Add($"With a {Math.Round(context.NoShowRate * 100, 0)}% historical no-show rate, expect roughly {expectedNext7:0} of the {t.PredictedNext7DayAppointments:0} appointments predicted for the next 7 days to actually attend.");
            }

            var outlook = $"Based on this hospital's appointment history, patient volume is trending {direction} and is projected at roughly {t.PredictedNext30DayAppointments:0} appointments over the next 30 days if current patterns hold.";
            return new PatientVolumeInsightNarrative(outlook, insights);
        }

        private class GroqInsightPayload
        {
            public string? Outlook { get; set; }
            public List<string>? Insights { get; set; }
        }
    }
}
