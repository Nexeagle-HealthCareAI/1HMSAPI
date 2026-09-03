using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class GroqBillingInsightService : IBillingInsightService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ILogger<GroqBillingInsightService> _logger;

        public GroqBillingInsightService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqBillingInsightService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Groq:ApiKey"] ?? "gsk_dummy";
            _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
            _logger = logger;

            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        private bool IsKeyUnset => string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "gsk_dummy" || _apiKey.StartsWith("<", StringComparison.Ordinal);

        public async Task<BillingInsightNarrative> GenerateInsightsAsync(TrendSummary t)
        {
            if (IsKeyUnset)
            {
                _logger.LogWarning("Groq:ApiKey is not configured (billing insights) — returning fallback narrative");
                return FallbackNarrative(t);
            }

            var leaks = t.RevenueCategoryTrends.Where(c => c.IsLeak).ToList();
            var growing = t.RevenueCategoryTrends.Where(c => c.ChangePercent > 0).OrderByDescending(c => c.ChangePercent).ToList();

            var prompt =
                "You are a hospital financial analyst. You are given ALREADY-COMPUTED billing trend " +
                "numbers -- do not invent, recompute, or contradict any figure below. Your job is only " +
                "to explain them in plain language for a hospital administrator.\n\n" +
                $"7-day average daily revenue: {t.Avg7DayRevenue}\n" +
                $"30-day average daily revenue: {t.Avg30DayRevenue}\n" +
                $"7-day average daily expense: {t.Avg7DayExpense}\n" +
                $"30-day average daily expense: {t.Avg30DayExpense}\n" +
                $"Month-over-month revenue change: {t.MonthOverMonthRevenueChangePercent}%\n" +
                $"Month-over-month expense change: {t.MonthOverMonthExpenseChangePercent}%\n" +
                $"Predicted tomorrow's revenue: {t.PredictedTomorrowRevenue}\n" +
                $"Predicted next-7-day revenue: {t.PredictedNext7DayRevenue}\n" +
                $"Predicted next-30-day revenue: {t.PredictedNext30DayRevenue}\n" +
                $"Predicted next-30-day expense: {t.PredictedNext30DayExpense}\n" +
                $"Category trends (month-over-month % change): {string.Join(", ", t.RevenueCategoryTrends.Select(c => $"{c.CategoryCode}: {c.ChangePercent}%"))}\n" +
                $"Categories flagged as declining 10%+ (potential leaks): {(leaks.Count == 0 ? "none" : string.Join(", ", leaks.Select(c => c.CategoryCode)))}\n\n" +
                "Respond with JSON only, no markdown fences, in exactly this shape:\n" +
                "{\"outlook\": \"one sentence summarizing the overall trajectory\", " +
                "\"insights\": [\"3 to 5 short, specific, actionable sentences -- prioritize calling out any declining category as where money may be leaking, and any growing category as a strength\"]}";

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
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Groq billing-insights call failed with {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                    return FallbackNarrative(t);
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(responseString);
                var raw = jsonDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(raw)) return FallbackNarrative(t);

                raw = StripMarkdownFence(raw);
                var parsed = JsonSerializer.Deserialize<GroqInsightPayload>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.Outlook)) return FallbackNarrative(t);

                return new BillingInsightNarrative(parsed.Outlook, parsed.Insights ?? new List<string>());
            }
            catch (Exception ex)
            {
                // Never let a Groq/network hiccup break the analytics page -- fall back to a
                // deterministic, code-generated narrative built from the same trend numbers.
                _logger.LogWarning(ex, "Groq billing-insights call threw — returning fallback narrative");
                return FallbackNarrative(t);
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
        private static BillingInsightNarrative FallbackNarrative(TrendSummary t)
        {
            var insights = new List<string>();
            var direction = t.MonthOverMonthRevenueChangePercent >= 0 ? "up" : "down";
            insights.Add($"Revenue is {direction} {Math.Abs(t.MonthOverMonthRevenueChangePercent)}% month-over-month.");

            foreach (var leak in t.RevenueCategoryTrends.Where(c => c.IsLeak).Take(3))
                insights.Add($"{leak.CategoryCode} revenue is down {Math.Abs(leak.ChangePercent)}% over the last 30 days -- worth checking for pricing, discounting, or volume drops.");

            var topGrowth = t.RevenueCategoryTrends.Where(c => c.ChangePercent > 0).OrderByDescending(c => c.ChangePercent).FirstOrDefault();
            if (topGrowth != null)
                insights.Add($"{topGrowth.CategoryCode} is your fastest-growing category, up {topGrowth.ChangePercent}% month-over-month.");

            var outlook = $"Based on the last 90 days, daily revenue is trending {direction} -- projected at roughly {t.PredictedTomorrowRevenue:0} tomorrow, {t.PredictedNext7DayRevenue:0} over the next 7 days, and {t.PredictedNext30DayRevenue:0} over the next 30 days if current patterns hold.";
            return new BillingInsightNarrative(outlook, insights);
        }

        private class GroqInsightPayload
        {
            public string? Outlook { get; set; }
            public List<string>? Insights { get; set; }
        }
    }
}
