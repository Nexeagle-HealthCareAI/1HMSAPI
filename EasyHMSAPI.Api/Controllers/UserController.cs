using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [Route("user")]
    [ApiController]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<UserController> _logger;

        public UserController(IMediator mediator, ILogger<UserController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("get-user-details")]
        [Authorize]
        public async Task<ActionResult<UserSearchResponseModel>> GetUserDetails([FromQuery] Guid userId)
        {
            try
            {
                _logger.LogInformation("GetUserDetails API started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
                var request = new UserSearchRequestModel { UserId = userId, CallerUserId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User) };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetUserDetails API ended for userId: {UserId}", userId);

                if (response?.Forbidden == true)
                    return StatusCode(403, new { message = "You don't have permission to access this resource." });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving user details for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving user details" });
            }
        }

        [HttpPut("update-user-details")]
        [Authorize]
        public async Task<ActionResult<UserProfileUpdateResponseModel>> UpdateUserDetails([FromBody] UserProfileUpdateRequestModel request)
        {
            _logger.LogInformation("UpdateUserDetails API started at {Time} for userId: {UserId}", DateTime.UtcNow, request.UserId);
            try
            {
                if (request.UserId == Guid.Empty)
                {
                    return BadRequest(new { Message = "User ID is required and cannot be empty." });
                }

                // Identity comes from the verified JWT, never from the body.
                request.CallerUserId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateUserDetails API ended for userId: {UserId}", request.UserId);

                if (response.Forbidden)
                    return StatusCode(403, new { message = response.Message });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user details for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while updating user details" });
            }
        }

        [HttpGet("permissions")]
        [Authorize]
        public async Task<ActionResult<UserPermissionsResponseModel>> GetUserPermissions([FromQuery] Guid userId)
        {
            _logger.LogInformation("GetUserPermissions API started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                // CallerUserId is resolved from the verified JWT here, never trusted from the
                // client -- same pattern as AdminController.QuickAddUser stamping
                // request.CallerUserId. The handler uses it to enforce self-only access
                // (see UserPermissionsHandler's own comment for why).
                var callerId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User);
                UserPermissionsRequestModel request = new() { UserId = userId == Guid.Empty ? Guid.Empty : userId, CallerUserId = callerId };

                var response = await _mediator.Send(request);
                if (response?.Forbidden == true)
                {
                    return StatusCode(403, new { message = "You don't have permission to access this resource." });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving user permissions for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving user permissions", Error = ex.Message });
            }
        }

        [HttpPut("profile-picture/upload")]
        [Authorize]
        public async Task<IActionResult> Upload([FromForm] UploadProfilePictureRequestModel command)
        {
            _logger.LogInformation("UploadProfilePicture started at {Time} for userId: {UserId}", DateTime.UtcNow, command.UserId);
            try
            {
                command.CallerUserId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User);
                var result = await _mediator.Send(command);
                _logger.LogInformation("UploadProfilePicture ended for userId: {UserId}", command.UserId);

                if (result.Forbidden)
                    return StatusCode(403, new { message = "You don't have permission to change this profile picture." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadProfilePicture for userId: {UserId}", command.UserId);
                return StatusCode(500, new { Message = "An error occurred while uploading profile picture" });
            }
        }

        [HttpGet("profile-picture/{userId}")]
        [Authorize]
        public async Task<IActionResult> Get(Guid userId)
        {
            _logger.LogInformation("GetProfilePicture started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                var request = new GetProfilePictureRequestModel { UserId = userId, CallerUserId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User) };
                var result = await _mediator.Send(request);
                _logger.LogInformation("GetProfilePicture ended for userId: {UserId}", userId);

                if (result.Forbidden)
                    return StatusCode(403, new { message = "You don't have permission to access this resource." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProfilePicture for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving profile picture" });
            }
        }

        [HttpDelete("profile-picture/remove")]
        [Authorize]
        public async Task<IActionResult> Delete(DeleteProfilePictureRequestModel requestModel)
        {
            _logger.LogInformation("DeleteProfilePicture started at {Time} for userId: {UserId}", DateTime.UtcNow, requestModel.UserId);
            try
            {
                requestModel.CallerUserId = EasyHMSAPI.Api.Common.UserContextHelper.GetUserId(User);
                var result = await _mediator.Send(requestModel);
                _logger.LogInformation("DeleteProfilePicture ended for userId: {UserId}", requestModel.UserId);

                if (result.Forbidden)
                    return StatusCode(403, new { message = result.Message });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteProfilePicture for userId: {UserId}", requestModel.UserId);
                return StatusCode(500, new { Message = "An error occurred while deleting profile picture" });
            }
        }
    }
}
