using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<AbdmProfileShareService> _logger;
        private readonly string _gatewayBaseUrl;
        private readonly string _callbackBaseUrl;
        private readonly string _cmId;

        public AbdmProfileShareService(HttpClient httpClient, IAbdmGatewayService gatewayService, IConfiguration configuration, IHostEnvironment environment, ILogger<AbdmProfileShareService> logger)
        {
            _httpClient = httpClient;
            _gatewayService = gatewayService;
            _logger = logger;
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

            await AbdmHttp.SendAsync(_httpClient, _gatewayService, _logger,
                () => BuildRequestAsync(HttpMethod.Post, $"{_callbackBaseUrl}/v3/hip/patient/profile/on-share", payload, cancellationToken),
                cancellationToken);
        }

        public async Task<string> RegisterBridgeUrlAsync(string bridgeUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_gatewayBaseUrl))
                throw new InvalidOperationException("Abdm:GatewayBaseUrlProd must be configured before registering a bridge URL in Production.");

            var response = await AbdmHttp.SendAsync(_httpClient, _gatewayService, _logger,
                () => BuildRequestAsync(new HttpMethod("PATCH"), $"{_gatewayBaseUrl}/v3/bridge/url", new { url = bridgeUrl }, cancellationToken),
                cancellationToken);
            var body = response.BodyText;
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
            AbdmHttp.AddStandardHeaders(request, _cmId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }
    }
}
