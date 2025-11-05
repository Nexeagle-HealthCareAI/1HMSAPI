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
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdminController> _logger;
        public AdminController(IMediator mediator, ILogger<AdminController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("user-onboarding/invitations")]
        public async Task<ActionResult<InvitationCreateResponseModel>> CreateInvitation([FromQuery] string scope, [FromBody] InvitationCreateRequestModel? request)
        {
            _logger.LogInformation("CreateInvitation started at {Time} for scope: {Scope}", DateTime.UtcNow, scope);
            try
            {
                if (!string.Equals(scope, "new", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { Message = "Invalid scope for this route. Use scope=new or call POST admin/user-onboarding/invitations/manage for resend/revoke." });
                }

                if (request == null)
                {
                    return BadRequest(new { Message = "Request body is required for scope=new" });
                }

                if (request.InvitedByUserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "InvitedByUserId is required in payload." });
                }
                var response = await _mediator.Send(request);

                _logger.LogInformation("CreateInvitation ended for scope: {Scope}", scope);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in CreateInvitation for scope: {Scope}. Error: {Error}", scope, ex);
                return StatusCode(500, new { Message = "An error occurred while creating the invitation.", Error = ex.Message });
            }
        }

        [HttpPost("user-onboarding/invitations/manage")]
        public async Task<ActionResult<InvitationUpdateResponseModel>> ManageInvitation([FromQuery] Guid invitationId, [FromQuery] string scope, [FromQuery] Guid performedByUserId)
        {
            _logger.LogInformation("ManageInvitation started at {Time} for invitationId: {InvitationId}, scope: {Scope}", DateTime.UtcNow, invitationId, scope);
            try
            {
                if (invitationId == Guid.Empty)
                {
                    return BadRequest(new { Message = "invitationId is required for resend/revoke." });
                }

                if (!string.Equals(scope, "resend", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(scope, "revoke", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { Message = "Invalid scope. Use scope=resend or scope=revoke" });
                }

                var updateReq = new InvitationUpdateRequestModel
                {
                    InvitationId = invitationId,
                    Scope = scope,
                    PerformedByUserId = performedByUserId
                };

                var response = await _mediator.Send(updateReq);
                _logger.LogInformation("ManageInvitation ended for invitationId: {InvitationId}, scope: {Scope}", invitationId, scope);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in ManageInvitation for invitationId: {InvitationId}, scope: {Scope}. Error: {Error}", invitationId, scope, ex);
                return StatusCode(500, new { Message = "An error occurred while managing the invitation.", Error = ex.Message });
            }
        }

        [HttpGet("user-onboarding/invitations")]
        public async Task<ActionResult<InvitationListResponseModel>> GetInvitations([FromQuery] Guid hospitalId, [FromQuery] string scope = "all")
        {
            _logger.LogInformation("GetInvitations started at {Time} for hospitalId: {HospitalId}, scope: {Scope}", DateTime.UtcNow, hospitalId, scope);
            try
            {
                var req = new InvitationListRequestModel { HospitalId = hospitalId, Scope = scope };
                var resp = await _mediator.Send(req);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetInvitations for hospitalId: {HospitalId}, scope: {Scope}. Error: {Error}", hospitalId, scope, ex);
                return StatusCode(500, new { Message = "An error occurred while fetching invitations.", Error = ex.Message });
            }
        }

        [HttpGet("user-onboarding/validate")]
        [AllowAnonymous]
        public async Task<ActionResult<InvitationValidateResponseModel>> ValidateInvitation([FromQuery] string token)
        {
            _logger.LogInformation("ValidateInvitation started at {Time} for token: {Token}", DateTime.UtcNow, token);
            try
            {
                var req = new InvitationValidateRequestModel { Token = token };
                var resp = await _mediator.Send(req);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in ValidateInvitation for token: {Token}. Error: {Error}", token, ex);
                return StatusCode(500, new { Message = "An error occurred while validating the invitation token.", Error = ex.Message });
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
                if (request.PerformedByUserId == Guid.Empty) return BadRequest(new { Message = "performedByUserId is required." });

                var resp = await _mediator.Send(request);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in DeactivateUser. Error: {Error}", ex);
                return StatusCode(500, new { Message = "An error occurred while deactivating the user.", Error = ex.Message });
            }
        }

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
                _logger.LogError("Error in GetAllHospitalUsers for hospitalId: {HospitalId}. Error: {Error}", hospitalId, ex);
                return StatusCode(500, new { Message = "An error occurred while fetching hospital users.", Error = ex.Message });
            }
        }

        [HttpPost("user-onboarding/invited/update-user")]
        public async Task<ActionResult<InvitationMapUserResponseModel>> MapInvitedUser([FromBody] InvitationMapUserRequestModel request)
        {
            _logger.LogInformation("MapInvitedUser started at {Time}", DateTime.UtcNow);
            try
            {
                if (request == null) return BadRequest(new { Message = "Request body is required." });
                if (request.InvitationId == Guid.Empty) return BadRequest(new { Message = "invitationId is required." });
                if (request.UserId == Guid.Empty) return BadRequest(new { Message = "userId is required." });

                var resp = await _mediator.Send(request);

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in MapInvitedUser. Error: {Error}", ex);
                return StatusCode(500, new { Message = "An error occurred while mapping invited user.", Error = ex.Message });
            }
        }
    }
}
