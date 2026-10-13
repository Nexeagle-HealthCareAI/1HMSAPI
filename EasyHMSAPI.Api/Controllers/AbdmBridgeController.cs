using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Every ABDM callback to our bridge URL ({public base}/abdm-callback/{secret}) other than the M1
    /// profile share, which keeps its own, more specific route. Each delivery is recorded before
    /// anything reads its body, checked as the abdm-m2 skill sets out (bearer present; an answer
    /// answers a call we sent; otherwise the facility header names one of ours), and acknowledged
    /// at once — ABDM waits on the 2xx, and the work happens after it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [AllowAnonymous]
    [Route("abdm-callback/{secret}")]
    public class AbdmBridgeController : ControllerBase
    {
        // Consent notifications and health information requests are a few KB.
        private const int MaxBodyBytes = 256 * 1024;

        private readonly IConfiguration _configuration;
        private readonly IAbdmExchangeLog _log;
        private readonly IAbdmCallbackGuard _guard;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AbdmBridgeController> _logger;

        public AbdmBridgeController(IConfiguration configuration, IAbdmExchangeLog log, IAbdmCallbackGuard guard, IServiceScopeFactory scopeFactory, ILogger<AbdmBridgeController> logger)
        {
            _configuration = configuration;
            _log = log;
            _guard = guard;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("{**path}")]
        [RequestSizeLimit(MaxBodyBytes)]
        public async Task<IActionResult> Receive(string secret, string path, CancellationToken cancellationToken)
        {
            if (!SecretMatches(secret)) return NotFound();

            // Optional layers (source address allow-list, ABDM-signed JWT) when configured.
            var (allowed, reason) = await _guard.CheckAsync(HttpContext.Connection.RemoteIpAddress, Request.Headers.Authorization.FirstOrDefault(), cancellationToken);
            if (!allowed)
            {
                _logger.LogWarning("ABDM callback on {Path} refused: {Reason}.", path, reason);
                return NotFound();
            }

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken);

            var headers = Request.Headers
                .Select(h => new KeyValuePair<string, string>(h.Key, h.Value.ToString()))
                .ToList();

            var inbound = await _log.RecordInboundAsync(new AbdmInboundDelivery(path, headers, body), cancellationToken);
            _logger.LogInformation("ABDM callback {Path}: {Verdict}, answers {AnswersRequestId}, REQUEST-ID {RequestId}.",
                path, inbound.Verdict, inbound.AnswersRequestId ?? "none", inbound.HeaderRequestId ?? "none");

            if (inbound.Verdict == AbdmInboundVerdict.Accepted)
                _ = ProcessInBackground(inbound.InboundId);

            return inbound.Verdict switch
            {
                AbdmInboundVerdict.Accepted or AbdmInboundVerdict.Duplicate => Accepted(),
                AbdmInboundVerdict.NoBearer => Unauthorized(),
                // Not ours: logged and dropped, not stored, answered or retried.
                _ => NotFound()
            };
        }

        // The request scope ends with the 202, so the work gets its own scope and can never fail
        // the delivery ABDM is waiting on.
        private async Task ProcessInBackground(Guid inboundId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAbdmCallbackProcessor>();
                await processor.ProcessAsync(inboundId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing ABDM callback {InboundId} failed.", inboundId);
            }
        }

        private bool SecretMatches(string supplied)
        {
            var expected = _configuration["Abdm:CallbackSecret"];
            if (string.IsNullOrWhiteSpace(expected) || expected.StartsWith('<')) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
        }
    }
}
