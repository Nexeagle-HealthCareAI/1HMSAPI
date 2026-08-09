using EasyHMSAPI.Application.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Looks up usage/side-effect text for a generic ingredient against the FDA's free,
    /// unauthenticated openFDA Drug Label API - the actual clinical content neither the 1mg
    /// import nor RxNorm carries (RxNorm is pure naming/terminology). US-label data, so tried
    /// under the RxNorm-resolved US name first when the caller has one, falling back to the raw
    /// ingredient name otherwise. Same India/US naming caveat as RxNorm.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OpenFdaLabelService : IDrugLabelService
    {
        // Label sections can run to thousands of words of legal/regulatory text; keep the panel
        // readable rather than dumping the full section.
        private const int MaxTextLength = 600;

        private readonly HttpClient _httpClient;

        public OpenFdaLabelService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DrugLabelLookupResult> LookupLabelAsync(string ingredientName, CancellationToken cancellationToken)
        {
            var trimmed = ingredientName?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return new DrugLabelLookupResult { Found = false };

            var result = await SearchAsync("openfda.substance_name", trimmed, cancellationToken)
                ?? await SearchAsync("openfda.generic_name", trimmed, cancellationToken);

            return result ?? new DrugLabelLookupResult { Found = false };
        }

        private async Task<DrugLabelLookupResult?> SearchAsync(string field, string value, CancellationToken cancellationToken)
        {
            var query = Uri.EscapeDataString($"{field}:\"{value}\"");
            using var response = await _httpClient.GetAsync($"drug/label.json?search={query}&limit=1", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            var indications = FirstOrNull(first, "indications_and_usage");
            var adverse = FirstOrNull(first, "adverse_reactions") ?? FirstOrNull(first, "warnings_and_cautions");

            if (indications == null && adverse == null)
                return null;

            return new DrugLabelLookupResult
            {
                Found = true,
                IndicationsAndUsage = Truncate(indications),
                AdverseReactions = Truncate(adverse),
            };
        }

        private static string? FirstOrNull(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                return null;
            return arr[0].GetString();
        }

        private static string? Truncate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            return trimmed.Length > MaxTextLength ? trimmed[..MaxTextLength].TrimEnd() + "…" : trimmed;
        }
    }
}
