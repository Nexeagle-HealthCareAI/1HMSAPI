using EasyHMSAPI.Application.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Looks up a generic/salt ingredient name against NLM's free, unauthenticated RxNorm
    /// (RxNav) API - drug name -> RxCUI, then RxCUI -> available prescribable forms/strengths
    /// (the "SCD" concept group). Only makes sense against generic names, not Indian brand
    /// names, which RxNorm has no knowledge of. Callers should cache results (see
    /// RxNormIngredientCache) rather than call this per request - NLM asks for reasonable use
    /// and the ingredient set behind any medicine catalog is small and mostly static.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RxNormService : IRxNormService
    {
        private readonly HttpClient _httpClient;

        // RxNorm mostly follows WHO's INN (which India also uses), but a short, well-known list
        // of drugs use the US Adopted Name (USAN) instead - that's the actual naming gap, not a
        // wholesale mismatch. Tried only when the direct INN lookup below returns nothing.
        private static readonly Dictionary<string, string> InnToUsanSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Paracetamol"] = "Acetaminophen",
            ["Salbutamol"] = "Albuterol",
            ["Adrenaline"] = "Epinephrine",
            ["Noradrenaline"] = "Norepinephrine",
            ["Frusemide"] = "Furosemide",
            ["Amoxycillin"] = "Amoxicillin",
            ["Lignocaine"] = "Lidocaine",
            ["Rifampicin"] = "Rifampin",
            ["Chlorpheniramine"] = "Chlorpheniramine Maleate",
            ["Pethidine"] = "Meperidine",
            ["Glibenclamide"] = "Glyburide",
            ["Amitriptyline"] = "Amitriptyline Hydrochloride",
            ["Cetirizine"] = "Cetirizine Hydrochloride",
            ["Diclofenac"] = "Diclofenac Sodium",
        };

        public RxNormService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RxNormLookupResult> LookupIngredientAsync(string ingredientName, CancellationToken cancellationToken)
        {
            var trimmed = ingredientName?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return new RxNormLookupResult { Found = false };

            var rxcui = await FindRxCuiAsync(trimmed, cancellationToken);
            var matchedName = trimmed;

            if (rxcui == null && InnToUsanSynonyms.TryGetValue(trimmed, out var usanName))
            {
                rxcui = await FindRxCuiAsync(usanName, cancellationToken);
                matchedName = usanName;
            }

            if (rxcui == null)
                return new RxNormLookupResult { Found = false };

            var forms = await GetRelatedFormsAsync(rxcui, cancellationToken);
            return new RxNormLookupResult
            {
                Found = true,
                RxCui = rxcui,
                DisplayName = matchedName,
                AvailableForms = forms,
            };
        }

        private async Task<string?> FindRxCuiAsync(string name, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync($"drugs.json?name={Uri.EscapeDataString(name)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("drugGroup", out var drugGroup) ||
                !drugGroup.TryGetProperty("conceptGroup", out var conceptGroup) ||
                conceptGroup.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var group in conceptGroup.EnumerateArray())
            {
                if (group.TryGetProperty("conceptProperties", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in props.EnumerateArray())
                    {
                        if (prop.TryGetProperty("rxcui", out var rxcuiProp))
                            return rxcuiProp.GetString();
                    }
                }
            }
            return null;
        }

        private async Task<List<string>> GetRelatedFormsAsync(string rxcui, CancellationToken cancellationToken)
        {
            var forms = new List<string>();

            using var response = await _httpClient.GetAsync($"rxcui/{Uri.EscapeDataString(rxcui)}/allrelated.json", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return forms;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("allRelatedGroup", out var allRelatedGroup) ||
                !allRelatedGroup.TryGetProperty("conceptGroup", out var conceptGroup) ||
                conceptGroup.ValueKind != JsonValueKind.Array)
                return forms;

            foreach (var group in conceptGroup.EnumerateArray())
            {
                if (!group.TryGetProperty("tty", out var ttyProp) || ttyProp.GetString() != "SCD")
                    continue;

                if (!group.TryGetProperty("conceptProperties", out var props) || props.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var prop in props.EnumerateArray())
                {
                    if (prop.TryGetProperty("name", out var nameProp))
                    {
                        var name = nameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            forms.Add(name);
                    }
                }
            }
            return forms;
        }
    }
}
