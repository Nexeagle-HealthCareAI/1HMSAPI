using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.Services
{
    // Pushes a "currently serving token #X" update to the WhatsApp gateway's already-built inbound
    // receiver (POST /events/token-called, see WHatspp Backened/app/webhook.py) whenever
    // queue/{doctorId}/call or /skip changes who's being served. The gateway looks the appointment
    // up by its own local record of who booked it via WhatsApp and no-ops silently for any
    // appointment it doesn't recognize (e.g. one booked at the front desk) -- so this is safe to
    // call for every waiting/called appointment on a doctor's queue, not just ones known to have
    // come through the bot. Same X-Service-Key-style shared-secret pattern already used for the
    // easyHMSAPI <-> CMSAPI service-to-service call (referral code validation).
    public class WhatsAppQueueNotifier : IWhatsAppQueueNotifier
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppQueueNotifier> _logger;

        public WhatsAppQueueNotifier(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WhatsAppQueueNotifier> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private class TokenCalledEventPayload
        {
            [JsonPropertyName("eventId")] public string EventId { get; set; } = string.Empty;
            [JsonPropertyName("appointmentId")] public string AppointmentId { get; set; } = string.Empty;
            [JsonPropertyName("currentToken")] public int CurrentToken { get; set; }
            [JsonPropertyName("estimatedWaitMinutes")] public int? EstimatedWaitMinutes { get; set; }
        }

        public async Task NotifyTokenCalledAsync(Guid appointmentId, int currentToken, int? estimatedWaitMinutes, CancellationToken cancellationToken)
        {
            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            var internalToken = _configuration["WhatsAppBot:InternalEventsToken"];
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(internalToken))
            {
                _logger.LogWarning("WhatsAppBot:BaseUrl or InternalEventsToken is not configured; skipping token-called push.");
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var payload = new TokenCalledEventPayload
                {
                    EventId = Guid.NewGuid().ToString(),
                    AppointmentId = appointmentId.ToString(),
                    CurrentToken = currentToken,
                    EstimatedWaitMinutes = estimatedWaitMinutes,
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/events/token-called")
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Add("X-Internal-Token", internalToken);

                var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("token-called push failed with {StatusCode} for appointment {AppointmentId}.", response.StatusCode, appointmentId);
                }
            }
            catch (Exception ex)
            {
                // Never let a notification failure fail the underlying call/skip/mark-arrived action.
                _logger.LogError(ex, "Error pushing token-called event for appointment {AppointmentId}.", appointmentId);
            }
        }
    }
}
