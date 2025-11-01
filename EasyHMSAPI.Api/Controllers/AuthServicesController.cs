using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("auth")]
    [ApiController]
    public class AuthServicesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthServicesController> _logger;
        public AuthServicesController(IMediator mediator, ILogger<AuthServicesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
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
                if(string.IsNullOrEmpty(scope))
                {
                    return BadRequest(new { Success = false, Message = "Invalid scope provided." });
                }
                request.Scope = scope;
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
}
