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
                var request = new UserSearchRequestModel { UserId = userId };
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetUserPermissions API ended for userId: {UserId}", userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving user details for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving user details", Error = ex.Message });
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

                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateUserDetails API ended for userId: {UserId}", request.UserId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user details for userId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = "An error occurred while updating user details", Error = ex.Message });
            }
        }

        [HttpGet("permissions")]
        [Authorize]
        public async Task<ActionResult<UserPermissionsResponseModel>> GetUserPermissions([FromQuery] Guid userId)
        {
            _logger.LogInformation("GetUserPermissions API started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                UserPermissionsRequestModel request = new();
                if (userId == Guid.Empty)
                {
                    request.UserId = Guid.Empty;
                }
                else
                {
                    request.UserId = userId;
                }

                var response = await _mediator.Send(request);

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
                var result = await _mediator.Send(command);
                _logger.LogInformation("UploadProfilePicture ended for userId: {UserId}", command.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadProfilePicture for userId: {UserId}", command.UserId);
                return StatusCode(500, new { Message = "An error occurred while uploading profile picture", Error = ex.Message });
            }
        }

        [HttpGet("profile-picture/{userId}")]
        [Authorize]
        public async Task<IActionResult> Get(Guid userId)
        {
            _logger.LogInformation("GetProfilePicture started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                var request = new GetProfilePictureRequestModel { UserId = userId };
                var result = await _mediator.Send(request);
                _logger.LogInformation("GetProfilePicture ended for userId: {UserId}", userId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProfilePicture for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving profile picture", Error = ex.Message });
            }
        }

        [HttpDelete("profile-picture/remove")]
        [Authorize]
        public async Task<IActionResult> Delete(DeleteProfilePictureRequestModel requestModel)
        {
            _logger.LogInformation("DeleteProfilePicture started at {Time} for userId: {UserId}", DateTime.UtcNow, requestModel.UserId);
            try
            {
                var result = await _mediator.Send(requestModel);
                _logger.LogInformation("DeleteProfilePicture ended for userId: {UserId}", requestModel.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteProfilePicture for userId: {UserId}", requestModel.UserId);
                return StatusCode(500, new { Message = "An error occurred while deleting profile picture", Error = ex.Message });
            }
        }
    }
}
