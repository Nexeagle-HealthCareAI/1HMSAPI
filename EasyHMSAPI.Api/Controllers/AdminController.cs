using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [Route("admin")]
    [ApiController]
    [Authorize]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdminController> _logger;
        public AdminController(IMediator mediator, ILogger<AdminController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Direct "quick add" of a team member (no invitation link). Creates the user + role +
        // hospital membership (+ doctor profile) with an admin-set password.
        [HttpPost("users/quick-add")]
        public async Task<ActionResult<QuickAddUserResponseModel>> QuickAddUser([FromBody] QuickAddUserRequestModel request)
        {
            var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                request.LoggedInUserName = await EasyHMSAPI.Api.Common.UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QuickAddUser for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while adding the team member." });
            }
        }

        // Updates an existing team member's details and roles
        [HttpPut("users/update")]
        public async Task<ActionResult<AdminUpdateUserResponseModel>> UpdateUser([FromBody] AdminUpdateUserRequestModel request)
        {
            var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateUser for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating the team member." });
            }
        }

        // Resets an existing member's password to a fresh temporary one and returns it (once) so the
        // admin can re-share login details. The original password can't be recovered (only the hash is stored).
        [HttpPost("users/reset-credentials")]
        public async Task<ActionResult<ResetCredentialsResponseModel>> ResetCredentials([FromBody] ResetCredentialsRequestModel request)
        {
            var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetCredentials for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while resetting the password." });
            }
        }

        // Sends a just-added member their login details (mobile + password) over email and/or WhatsApp.
        [HttpPost("users/share-credentials")]
        public async Task<ActionResult<ShareCredentialsResponseModel>> ShareCredentials([FromBody] ShareCredentialsRequestModel request)
        {
            var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShareCredentials for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while sending the login details." });
            }
        }

        [HttpPatch("user-onboarding/deactivate-user")]
        public async Task<ActionResult<DeactivateUserResponseModel>> DeactivateUser([FromBody] DeactivateUserRequestModel? request)
        {
            _logger.LogInformation("DeactivateUser started at {Time}", DateTime.UtcNow);
            try
            {
                if (request == null) return BadRequest(new { Message = "Request body is required." });
                if (request.HospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
                if (request.UserId == Guid.Empty) return BadRequest(new { Message = "userId is required." });

                var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
                if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
                request.CallerUserId = userId.Value;

                var resp = await _mediator.Send(request);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeactivateUser for hospitalId: {HospitalId}", request?.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while deactivating the user." });
            }
        }

        [HttpPatch("user-onboarding/reactivate-user")]
        public async Task<ActionResult<ReactivateUserResponseModel>> ReactivateUser([FromBody] ReactivateUserRequestModel? request)
        {
            _logger.LogInformation("ReactivateUser started at {Time}", DateTime.UtcNow);
            try
            {
                if (request == null) return BadRequest(new { Message = "Request body is required." });
                if (request.HospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
                if (request.UserId == Guid.Empty) return BadRequest(new { Message = "userId is required." });

                var userId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(HttpContext.User);
                if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
                request.CallerUserId = userId.Value;

                var resp = await _mediator.Send(request);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReactivateUser for hospitalId: {HospitalId}", request?.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while reactivating the user." });
            }
        }

        // Public API keys (for the Nexeagle booking website) are no longer issued via this
        // self-service endpoint — a key is now platform-wide (spans every opted-in hospital),
        // so any hospital admin minting one would be a privilege escalation. Keys are created
        // via the tools/IssuePublicApiKey ops console tool instead — see its runbook.

        [HttpGet("user-onboarding/allusers")]
        public async Task<ActionResult<HospitalUsersListResponseModel>> GetAllHospitalUsers([FromQuery] Guid hospitalId)
        {
            _logger.LogInformation("GetAllHospitalUsers started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
                var req = new HospitalUsersListRequestModel { HospitalId = hospitalId };
                var resp = await _mediator.Send(req);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllHospitalUsers for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching hospital users." });
            }
        }
    }
}
