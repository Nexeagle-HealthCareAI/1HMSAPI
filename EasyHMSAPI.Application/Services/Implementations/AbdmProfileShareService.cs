using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Outbound half of ABDM's "Scan Health Facility QR" flow. UNVERIFIED against a live sandbox:
    /// the public docs give the on-share path and headers but not its JSON body, and list its base
    /// URL as ".../hiecm/api" while the session/ABHA calls use ".../api/hiecm/gateway". Both are
    /// therefore configurable (Abdm:HipCallbackBaseUrl) and the body shape follows the earlier
    /// on-share convention — if ABDM rejects it, the error text is logged verbatim and this is the
    /// one file to correct.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbdmProfileShareService : IAbdmProfileShareService
    {
        private readonly HttpClient _httpClient;
        private readonly IAbdmGatewayService _gatewayService;
        private readonly string _gatewayBaseUrl;
        private readonly string _callbackBaseUrl;
        private readonly string _cmId;

        public AbdmProfileShareService(HttpClient httpClient, IAbdmGatewayService gatewayService, IConfiguration configuration, IHostEnvironment environment)
        {
            _httpClient = httpClient;
            _gatewayService = gatewayService;
            var isProd = environment.IsProduction();
            _cmId = isProd ? "abdm" : "sbx";

            var gateway = configuration[isProd ? "Abdm:GatewayBaseUrlProd" : "Abdm:GatewayBaseUrl"];
            _gatewayBaseUrl = !string.IsNullOrWhiteSpace(gateway)
                ? gateway.TrimEnd('/')
                : !isProd ? "https://dev.abdm.gov.in/api/hiecm/gateway" : string.Empty;

            var callback = configuration["Abdm:HipCallbackBaseUrl"];
            _callbackBaseUrl = !string.IsNullOrWhiteSpace(callback) ? callback.TrimEnd('/') : _gatewayBaseUrl;
        }

        public async Task AcknowledgeAsync(string callbackRequestId, string? abhaAddress, bool accepted, string? errorMessage, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_callbackBaseUrl))
                throw new InvalidOperationException("Abdm:HipCallbackBaseUrl (or GatewayBaseUrlProd) must be configured to acknowledge profile shares.");

            var payload = new
            {
                acknowledgement = new { status = accepted ? "SUCCESS" : "FAILURE", abhaAddress },
                error = accepted ? null : new { code = 1000, message = errorMessage ?? "Profile share could not be processed." },
                response = new { requestId = callbackRequestId }
            };

            using var request = await BuildRequestAsync(HttpMethod.Post, $"{_callbackBaseUrl}/v3/hip/patient/profile/on-share", payload, cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"ABDM on-share acknowledgement failed ({(int)response.StatusCode}): {body}");
            }
        }

        public async Task<string> RegisterBridgeUrlAsync(string bridgeUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_gatewayBaseUrl))
                throw new InvalidOperationException("Abdm:GatewayBaseUrlProd must be configured before registering a bridge URL in Production.");

            using var request = await BuildRequestAsync(new HttpMethod("PATCH"), $"{_gatewayBaseUrl}/v3/bridge/url", new { url = bridgeUrl }, cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"ABDM bridge URL registration failed ({(int)response.StatusCode}): {body}");
            return string.IsNullOrWhiteSpace(body) ? "Registered." : body;
        }

        private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url, object payload, CancellationToken cancellationToken)
        {
            var accessToken = await _gatewayService.GetAccessTokenAsync(cancellationToken);
            var request = new HttpRequestMessage(method, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
            request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("O"));
            request.Headers.Add("X-CM-ID", _cmId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }
    }
}
