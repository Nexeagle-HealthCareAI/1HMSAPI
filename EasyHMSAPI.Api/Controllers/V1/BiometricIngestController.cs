using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EasyHMSAPI.Api.Controllers.V1
{
    /// <summary>The scans a device (or an on-site bridge) is delivering.</summary>
    public class DevicePunchBatchBody
    {
        public List<BiometricPunchDto>? Punches { get; set; }
    }

    /// <summary>
    /// Device-facing push endpoint. Devices can't sign in, so they authenticate with their registered
    /// serial number and access token (headers X-Device-Serial / X-Device-Token) instead of a user JWT.
    /// Used by anything that can speak JSON: a custom device, an on-site bridge for a ZKTeco terminal that
    /// can only be polled, or a test client. (ZKTeco's own push protocol is served by ZktecoPushController.)
    /// </summary>
    [Route("api/v1/hr/biometric")]
    [ApiController]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("PerIpPolicy")]
    public class BiometricIngestController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;

        public BiometricIngestController(IMediator mediator, IConfiguration configuration)
        {
            _mediator = mediator;
            _configuration = configuration;
        }

        [HttpPost("punches")]
        public async Task<ActionResult<IngestBiometricPunchesResponseModel>> Punches(
            [FromBody] DevicePunchBatchBody body,
            [FromHeader(Name = "X-Device-Serial")] string? deviceSerial,
            [FromHeader(Name = "X-Device-Token")] string? deviceToken)
        {
            var result = await _mediator.Send(new IngestBiometricPunchesRequestModel
            {
                DeviceSerial = deviceSerial,
                DeviceToken = deviceToken,
                RemoteIp = TrustedProxyIpResolver.Resolve(HttpContext, _configuration["Internal:ProxyForwardingSecret"]),
                Punches = body.Punches ?? new List<BiometricPunchDto>(),
            });

            if (result.Unauthorized) return Unauthorized(new { message = result.Message });
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
