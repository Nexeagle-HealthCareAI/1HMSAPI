using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class GroqPatientChurnInsightService : IPatientChurnInsightService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GroqPatientChurnInsightService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Groq:ApiKey"] ?? "gsk_dummy";
            _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<PatientChurnNarrative> GenerateInsightsAsync(PatientChurnSummary summary)
        {
            if (_apiKey == "gsk_dummy") return FallbackNarrative(summary);
            if (summary.LapsedCount == 0) return new PatientChurnNarrative("No patients currently look lapsed -- everyone with a regular visiting pattern has returned within their usual rhythm.", string.Empty);

            var prompt =
                "You are a patient-engagement assistant for a hospital. You are given ONLY aggregate, " +
                "already-computed counts -- you do not know any patient's name or identity, and must " +
                "never invent one. Do not invent or contradict any number below.\n\n" +
                $"Number of patients who look lapsed (used to visit regularly, haven't returned): {summary.LapsedCount}\n" +
                $"Of those, number who have opted in to marketing contact: {summary.ConsentedLapsedCount}\n" +
                $"Specialties they most commonly used to visit: {(summary.TopSpecialtiesTheyUsedToVisit.Count == 0 ? "unknown" : string.Join(", ", summary.TopSpecialtiesTheyUsedToVisit))}\n\n" +
                "Respond with JSON only, no markdown fences, in exactly this shape:\n" +
                "{\"outlook\": \"one sentence summarizing the situation using only the numbers given\", " +
                "\"suggestedOutreachMessage\": \"a short, warm, GENERIC re-engagement message template staff can adapt and send -- do not address it to any named person, use a placeholder like [Patient Name] instead\"}";

            var requestBody = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.4,
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
                if (string.IsNullOrWhiteSpace(raw)) return FallbackNarrative(summary);

                raw = StripMarkdownFence(raw);
                var parsed = JsonSerializer.Deserialize<GroqChurnPayload>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.Outlook)) return FallbackNarrative(summary);

                return new PatientChurnNarrative(parsed.Outlook, parsed.SuggestedOutreachMessage ?? string.Empty);
            }
            catch (Exception)
            {
                // Never let a Groq/network hiccup break the dashboard -- fall back to a
                // deterministic, code-generated narrative and template.
                return FallbackNarrative(summary);
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

        /// <summary>Used both for the no-API-key dummy mode and as a resilience fallback on any Groq failure.</summary>
        private static PatientChurnNarrative FallbackNarrative(PatientChurnSummary summary)
        {
            var outlook = $"{summary.LapsedCount} patient(s) who used to visit regularly haven't returned within their usual pattern, {summary.ConsentedLapsedCount} of whom have opted in to marketing contact.";
            var template = "Hi [Patient Name], it's been a while since your last visit with us. We hope you're doing well -- if you're due for a check-up or have any concerns, we'd love to see you again. Reply to this message or call us to book an appointment.";
            return new PatientChurnNarrative(outlook, template);
        }

        private class GroqChurnPayload
        {
            public string? Outlook { get; set; }
            public string? SuggestedOutreachMessage { get; set; }
        }
    }
}
