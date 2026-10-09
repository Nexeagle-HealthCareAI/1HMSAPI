using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Health Wiki contributors (independent doctors, health workers, writers): invitation link, WhatsApp code sign-in.
    /// Called by the Doctor Dekho server on behalf of the contributor pages; the browser never calls it directly.
    /// Mobile numbers are never returned in full. Further endpoints (profile, articles, reviews, topics, images) are added
    /// here behind ContributorSessionFilter.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("contributor")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("ContributorAuthPolicy")]
    public class ContributorController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ContributorController> _logger;

        public ContributorController(IMediator mediator, ILogger<ContributorController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public class OtpSendBody
        {
            public string? Mobile { get; set; }
            public string? InviteToken { get; set; }
        }

        public class OtpVerifyBody
        {
            public string? Mobile { get; set; }
            public string? Code { get; set; }
            public string? InviteToken { get; set; }
            public string? Role { get; set; }
        }

        [HttpGet("invite/{token}")]
        public async Task<IActionResult> Invite(string token)
        {
            try
            {
                var res = await _mediator.Send(new GetContributorInviteRequestModel { Token = token });
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading a contributor invitation");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // "Join as a contributor" without an invitation: the same code step as sign-in.
        [HttpPost("enrol")]
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp([FromBody] OtpSendBody body)
        {
            try
            {
                var res = await _mediator.Send(new ContributorOtpSendRequestModel
                {
                    Mobile = body?.Mobile, InviteToken = body?.InviteToken, RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                });
                if (res.RetryAfterSeconds.HasValue) Response.Headers["Retry-After"] = res.RetryAfterSeconds.Value.ToString();
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message, retryAfterSeconds = res.RetryAfterSeconds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending a contributor code");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body)
        {
            try
            {
                var res = await _mediator.Send(new ContributorOtpVerifyRequestModel
                {
                    Mobile = body?.Mobile, Code = body?.Code, InviteToken = body?.InviteToken, Role = body?.Role,
                });
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying a contributor code");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("logout")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var sessionId = ContributorSessionFilter.SessionId(HttpContext);
                if (sessionId.HasValue) await _mediator.Send(new ContributorLogoutRequestModel { SessionId = sessionId.Value });
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing a contributor out");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }
    }
}
