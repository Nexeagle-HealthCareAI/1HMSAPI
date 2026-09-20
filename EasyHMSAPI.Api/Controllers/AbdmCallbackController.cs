using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Inbound ABDM callbacks for the "Scan Health Facility QR" flow. ABDM appends its fixed path to
    /// the bridge URL we register, so that URL is <c>{public base}/abdm-callback/{secret}</c>.
    /// ABDM's own callback auth scheme isn't published, and an open endpoint that stores patient
    /// profiles is not acceptable, so the unguessable secret path segment is the gate (a wrong or
    /// unconfigured secret is a plain 404 — the endpoint doesn't reveal that it exists).
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [AllowAnonymous]
    [Route("abdm-callback/{secret}/v3/hip/patient/profile")]
    public class AbdmCallbackController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AbdmCallbackController> _logger;

        public AbdmCallbackController(IMediator mediator, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<AbdmCallbackController> logger)
        {
            _mediator = mediator;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("share")]
        public async Task<IActionResult> Share(string secret, CancellationToken cancellationToken)
        {
            if (!SecretMatches(secret)) return NotFound();

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken);

            try
            {
                var result = await _mediator.Send(new RecordAbdmProfileShareRequestModel
                {
                    RawBody = body,
                    HeaderRequestId = Request.Headers["REQUEST-ID"].FirstOrDefault()
                }, cancellationToken);

                if (!string.IsNullOrWhiteSpace(result.CallbackRequestId))
                    _ = AcknowledgeInBackground(result.CallbackRequestId!, result.AbhaAddress, result.Accepted, result.Message);

                // ABDM only needs to know the callback arrived; the outcome travels in on-share.
                return Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording ABDM profile share.");
                return StatusCode(500);
            }
        }

        private bool SecretMatches(string supplied)
        {
            var expected = _configuration["Abdm:CallbackSecret"];
            // A checked-in "<set-via-env-…>" placeholder must never work as a real secret.
            if (string.IsNullOrWhiteSpace(expected) || expected.StartsWith('<')) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
        }

        // The request scope ends when we return 202, so the ack gets its own scope and never fails
        // the callback itself.
        private async Task AcknowledgeInBackground(string callbackRequestId, string? abhaAddress, bool accepted, string? message)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAbdmProfileShareService>();
                await service.AcknowledgeAsync(callbackRequestId, abhaAddress, accepted, message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ABDM on-share acknowledgement failed for callback {RequestId}.", callbackRequestId);
            }
        }
    }
}
