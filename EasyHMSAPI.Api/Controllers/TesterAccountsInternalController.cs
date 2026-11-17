using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Reusable test accounts for sign-up and onboarding (docs/TESTER-ACCOUNTS.md). Development servers only:
    /// anywhere else, or with no StaffAuth:TesterResetKey set, every route here answers 404 as if it did not exist.
    /// The caller sends the key in the X-Tester-Key header. A signed-in tester can also reset their own account from the
    /// Dev UI (me, reset-me): no key is needed there, because the account is found from the sign-in, not from the request.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("internal/tester-accounts")]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("PerIpPolicy")]
    public class TesterAccountsInternalController : ControllerBase
    {
        public const string HeaderName = "X-Tester-Key";

        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TesterAccountsInternalController> _logger;

        public TesterAccountsInternalController(IMediator mediator, IConfiguration configuration, ILogger<TesterAccountsInternalController> logger)
        {
            _mediator = mediator;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Deletes a tester number's account, the hospitals it registered and everything under them, so the number
        /// can sign up again. Body: { "mobileNumber": "9000000001", "dryRun": true } (dryRun only reports).
        /// </summary>
        [HttpPost("reset")]
        [AllowAnonymous]
        public async Task<ActionResult<ResetTesterAccountResponseModel>> Reset([FromBody] ResetTesterAccountRequestModel request)
        {
            var expected = TesterAccounts.ResetKey(_configuration);
            if (expected == null) return NotFound();

            var provided = Request.Headers[HeaderName].ToString();
            var ok = !string.IsNullOrEmpty(provided) && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
            if (!ok) return Unauthorized(new { message = "Invalid or missing tester key." });

            try
            {
                var response = await _mediator.Send(request ?? new ResetTesterAccountRequestModel());
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TesterAccountsInternalController.Reset");
                return StatusCode(500, new { message = "The reset failed." });
            }
        }

        /// <summary>Is the signed-in user a tester on this Development server? The Dev UI asks before offering a reset. 404 elsewhere.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<TesterAccountStatusResponseModel>> Me()
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            var status = await _mediator.Send(new TesterAccountStatusRequestModel { UserId = userId.Value });
            if (!status.Available) return NotFound();
            return Ok(status);
        }

        /// <summary>
        /// The signed-in tester resets their own account (Dev UI). Body: { "dryRun": true } to only see what would go.
        /// Same rules as reset: Development only, tester numbers only, one transaction.
        /// </summary>
        [HttpPost("reset-me")]
        [Authorize]
        public async Task<ActionResult<ResetTesterAccountResponseModel>> ResetMe([FromBody] ResetTesterAccountRequestModel? request)
        {
            if (!TesterAccounts.IsDevelopment(_configuration)) return NotFound();
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var response = await _mediator.Send(new ResetTesterAccountRequestModel { UserId = userId.Value, DryRun = request?.DryRun ?? false });
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TesterAccountsInternalController.ResetMe");
                return StatusCode(500, new { message = "The reset failed." });
            }
        }
    }
}
