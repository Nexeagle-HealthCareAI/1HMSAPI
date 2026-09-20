using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [Route("auth")]
    [ApiController]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    public class AuthServicesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthServicesController> _logger;
        private readonly IMagicLinkService _magicLinkService;
        private readonly IConfiguration _configuration;
        public AuthServicesController(IMediator mediator, ILogger<AuthServicesController> logger, IMagicLinkService magicLinkService, IConfiguration configuration)
        {
            _mediator = mediator;
            _logger = logger;
            _magicLinkService = magicLinkService;
            _configuration = configuration;
        }

        [HttpPost("user/login")]
        public async Task<ActionResult<UserLoginResponseModel>> Login([FromBody] UserLoginRequestModel request)
        {
            _logger.LogInformation("Login started at {Time}", DateTime.UtcNow);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("Login ended");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Login");
                return StatusCode(500, new { Message = "An error occurred during login", Error = ex.Message });
            }
        }

        // Redeems the single-use token from a WhatsApp/email "log in to view" link for a normal
        // session. POST (never GET) on purpose: link-preview crawlers and mail scanners only issue
        // GETs, so they can't burn a recipient's one-time link. Failures return 200 + Success=false
        // with one generic message, mirroring Login above, so the client shows a friendly retry path.
        [HttpPost("magic-link/exchange")]
        [EnableRateLimiting("MagicLinkPolicy")]
        public async Task<IActionResult> ExchangeMagicLink([FromBody] MagicLinkExchangeRequest request)
        {
            try
            {
                var clientIp = EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(HttpContext, _configuration["Internal:ProxyForwardingSecret"]);
                var result = await _magicLinkService.ExchangeAsync(request?.Token ?? string.Empty, clientIp, HttpContext.RequestAborted);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExchangeMagicLink");
                return StatusCode(500, new { Message = "An error occurred during login" });
            }
        }

        [HttpPost("user/register")]
        public async Task<ActionResult<UserRegistrationResponseModel>> Signup([FromBody] UserRegistrationRequestModel request)
        {
            _logger.LogInformation("Signup started at {Time}", DateTime.UtcNow);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("Signup ended");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Signup");
                return StatusCode(500, new { Message = "An error occurred during signup", Error = ex.Message });
            }
        }

        [HttpPost("otp/send")]
        public async Task<ActionResult<OtpSendResponseModel>> OtpGenerater([FromBody] OtpSendRequestModel request)
        {
            _logger.LogInformation("OtpGenerater started at {Time} for mobile: {MobileNumber}", DateTime.UtcNow, request.MobileNumber);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("OtpGenerater ended for mobile: {MobileNumber}", request.MobileNumber);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OtpGenerater for mobile: {MobileNumber}", request.MobileNumber);
                return StatusCode(500, new { Message = "An error occurred while sending OTP", Error = ex.Message });
            }
        }

        [HttpPost("otp/verify")]
        public async Task<ActionResult<OtpVerifyResponseModel>> OtpChecker([FromBody] OtpVerifyRequestModel request)
        {
            _logger.LogInformation("OtpChecker started at {Time} for mobile: {MobileNumber}", DateTime.UtcNow, request.MobileNumber);
            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("OtpChecker ended for mobile: {MobileNumber}", request.MobileNumber);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OtpChecker for mobile: {MobileNumber}", request.MobileNumber);
                return StatusCode(500, new { Message = "An error occurred while verifying OTP", Error = ex.Message });
            }
        }

        [HttpPatch("user/password")]
        [Authorize]
        public async Task<IActionResult> SetOrResetPassword([FromQuery] string scope, [FromBody] SetOrResetPasswordRequestModel request)
        {
            _logger.LogInformation("SetOrResetPassword started at {Time} for scope: {Scope}", DateTime.UtcNow, scope);
            try
            {
                request.Scope = scope;
                // Changing your own password from an authenticated session must act on the
                // CALLER's account — never trust a client-supplied UserId for this scope.
                if (string.Equals(scope, "change-password", StringComparison.OrdinalIgnoreCase))
                {
                    var callerId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User);
                    if (callerId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
                    request.UserId = callerId.Value;
                }
                var result = await _mediator.Send(request);
                _logger.LogInformation("SetOrResetPassword ended for scope: {Scope}", scope);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SetOrResetPassword for scope: {Scope}", scope);
                return StatusCode(500, new { Message = "An error occurred while setting or resetting password", Error = ex.Message });
            }
        }
    }

    public class MagicLinkExchangeRequest
    {
        public string? Token { get; set; }
    }
}
