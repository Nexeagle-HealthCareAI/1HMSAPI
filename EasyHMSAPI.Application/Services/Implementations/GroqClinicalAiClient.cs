using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Chat completions on Groq (OpenAI-compatible API). Temperature is kept low because the output
    /// is clinical documentation the doctor reviews, not creative text.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class GroqClinicalAiClient : IClinicalAiClient
    {
        private readonly HttpClient _httpClient;
        private readonly AiProviderConfig _config;

        public GroqClinicalAiClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(90);
            _config = AiProviderConfig.Resolve(configuration);
        }

        public bool IsConfigured => _config.IsConfigured;
        public string NotConfiguredMessage => _config.NotConfiguredMessage;

        public Task<string> CompleteJsonAsync(string systemPrompt, string userContent, CancellationToken cancellationToken) =>
            SendAsync(systemPrompt, userContent, json: true, cancellationToken);

        public Task<string> CompleteTextAsync(string systemPrompt, string userContent, CancellationToken cancellationToken) =>
            SendAsync(systemPrompt, userContent, json: false, cancellationToken);

        private async Task<string> SendAsync(string systemPrompt, string userContent, bool json, CancellationToken cancellationToken)
        {
            if (!_config.IsConfigured)
                throw new InvalidOperationException(_config.NotConfiguredMessage);

            var messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent },
            };
            object payload = json
                ? new { model = _config.ChatModel, temperature = 0.2, response_format = new { type = "json_object" }, messages }
                : new { model = _config.ChatModel, temperature = 0.2, messages };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"The AI service did not answer ({(int)response.StatusCode}). Please try again.");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? string.Empty;
        }
    }
}
