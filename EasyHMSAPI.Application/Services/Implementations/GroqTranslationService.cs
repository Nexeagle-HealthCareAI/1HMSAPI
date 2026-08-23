using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class GroqTranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GroqTranslationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // Fallback to empty to avoid crashing if not set, handled in method
            _apiKey = configuration["Groq:ApiKey"] ?? "gsk_dummy";
            _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
            
            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (_apiKey == "gsk_dummy") return $"(Mock translation to {targetLanguage}) {text}";

            var prompt = $"Translate the following medical text to {targetLanguage}. Only output the translated text and nothing else. Do not add any introductory or concluding remarks.\n\nText: {text}";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);
            
            var translatedText = jsonDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return translatedText?.Trim() ?? text;
        }

        public async Task<Dictionary<string, string>> TranslateMultipleAsync(Dictionary<string, string> texts, string targetLanguage)
        {
            if (texts == null || texts.Count == 0) return new Dictionary<string, string>();
            if (_apiKey == "gsk_dummy") 
            {
                var dict = new Dictionary<string, string>();
                foreach (var kvp in texts)
                {
                    dict[kvp.Key] = string.IsNullOrWhiteSpace(kvp.Value) ? kvp.Value : $"(Mock translation to {targetLanguage}) {kvp.Value}";
                }
                return dict;
            }

            var result = new Dictionary<string, string>();
            var combinedTextBuilder = new StringBuilder();
            
            // Build a JSON mapping of Key -> Text for bulk translation
            var keys = new List<string>();
            var index = 0;
            var requestMap = new Dictionary<string, string>();
            
            foreach (var kvp in texts)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    result[kvp.Key] = kvp.Value;
                    continue;
                }
                requestMap[kvp.Key] = kvp.Value;
                keys.Add(kvp.Key);
            }

            if (requestMap.Count == 0) return result;

            var jsonInput = JsonSerializer.Serialize(requestMap);
            var prompt = $"Translate the values of the following JSON object to {targetLanguage}. Keep the keys exactly the same. Only output valid JSON and nothing else. Do not wrap in markdown.\n\n{jsonInput}";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);
            
            var translatedJsonStr = jsonDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (!string.IsNullOrWhiteSpace(translatedJsonStr))
            {
                // Strip potential markdown blocks
                translatedJsonStr = translatedJsonStr.Trim();
                if (translatedJsonStr.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    translatedJsonStr = translatedJsonStr.Substring(7);
                }
                else if (translatedJsonStr.StartsWith("```"))
                {
                    translatedJsonStr = translatedJsonStr.Substring(3);
                }
                
                if (translatedJsonStr.EndsWith("```"))
                {
                    translatedJsonStr = translatedJsonStr.Substring(0, translatedJsonStr.Length - 3);
                }
                
                translatedJsonStr = translatedJsonStr.Trim();

                try
                {
                    var translatedMap = JsonSerializer.Deserialize<Dictionary<string, string>>(translatedJsonStr);
                    if (translatedMap != null)
                    {
                        foreach (var kvp in translatedMap)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse JSON from Groq: {ex.Message}. Response was: {translatedJsonStr}");
                }
            }

            // Fill any missing keys with original text just in case
            foreach (var key in keys)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = requestMap[key];
                }
            }

            return result;
        }
    }
}
