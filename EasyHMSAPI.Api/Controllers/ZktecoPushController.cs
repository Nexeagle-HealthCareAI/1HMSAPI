using System.Text;
using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// ZKTeco "push" (ADMS / iClock) endpoints. The terminal calls out to a server address entered in its
    /// Cloud Server menu, using these fixed paths, so they live at /iclock/* rather than under /api/v1.
    /// Responses are plain text, as the device expects. See ZktecoPushService for the protocol notes and
    /// the caveat that this is not yet verified against a physical device.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("PerIpPolicy")]
    [Route("iclock")]
    public class ZktecoPushController : ControllerBase
    {
        private readonly IZktecoPushService _service;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ZktecoPushController> _logger;

        public ZktecoPushController(IZktecoPushService service, IConfiguration configuration, ILogger<ZktecoPushController> logger)
        {
            _service = service;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>Start-up handshake: the device asks what options to run with.</summary>
        [HttpGet("cdata")]
        public async Task<IActionResult> Handshake([FromQuery(Name = "SN")] string? serial, CancellationToken cancellationToken)
            => AsText(await _service.HandshakeAsync(serial, RemoteIp(), cancellationToken));

        /// <summary>A batch of records (attendance logs, operation logs, ...).</summary>
        [HttpPost("cdata")]
        public async Task<IActionResult> Upload(
            [FromQuery(Name = "SN")] string? serial,
            [FromQuery(Name = "table")] string? table,
            [FromQuery(Name = "Stamp")] string? stamp,
            CancellationToken cancellationToken)
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken);

            return AsText(await _service.UploadAsync(serial, table, stamp, body, RemoteIp(), cancellationToken));
        }

        /// <summary>The device polling for commands, and reporting their results. We have none to give.</summary>
        [HttpGet("getrequest")]
        [HttpPost("getrequest")]
        [HttpPost("devicecmd")]
        [HttpGet("ping")]
        public async Task<IActionResult> Touch([FromQuery(Name = "SN")] string? serial, CancellationToken cancellationToken)
            => AsText(await _service.TouchAsync(serial, RemoteIp(), cancellationToken));

        /// <summary>
        /// Anything else a device calls (newer firmware also uses /registry and /push). Logged so the first
        /// real connection shows exactly which paths it wants, then acknowledged so it isn't left retrying a 404.
        /// </summary>
        [AcceptVerbs("GET", "POST", Route = "{**rest}")]
        public async Task<IActionResult> Other(string? rest, [FromQuery(Name = "SN")] string? serial, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Unhandled ZKTeco request {Method} /iclock/{Rest}{Query}", Request.Method, rest, Request.QueryString);
            return AsText(await _service.TouchAsync(serial, RemoteIp(), cancellationToken));
        }

        private string RemoteIp() => TrustedProxyIpResolver.Resolve(HttpContext, _configuration["Internal:ProxyForwardingSecret"]);

        private static ContentResult AsText(ZktecoPushResult result) =>
            new() { Content = result.Body, ContentType = "text/plain", StatusCode = result.StatusCode };
    }
}
