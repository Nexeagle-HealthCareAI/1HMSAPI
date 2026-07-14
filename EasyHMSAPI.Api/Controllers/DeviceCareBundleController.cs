using EasyHMSAPI.Api.Common;
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
    // CLABSI/CAUTI/VAP daily care-bundle compliance checks against an active device.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("devices/bundle-check")]
    [Authorize]
    public class DeviceCareBundleController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DeviceCareBundleController> _logger;

        public DeviceCareBundleController(IMediator mediator, ILogger<DeviceCareBundleController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetDeviceCareBundleChecksResponseModel>> GetChecks([FromQuery] Guid hospitalId, [FromQuery] Guid deviceAssignmentId)
        {
            if (hospitalId == Guid.Empty || deviceAssignmentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and deviceAssignmentId are required." });

            try
            {
                var response = await _mediator.Send(new GetDeviceCareBundleChecksRequestModel { HospitalId = hospitalId, DeviceAssignmentId = deviceAssignmentId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetChecks for deviceAssignmentId: {DeviceAssignmentId}", deviceAssignmentId);
                return StatusCode(500, new { Message = "An error occurred while loading bundle checks." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<SubmitDeviceCareBundleCheckResponseModel>> Submit([FromBody] SubmitDeviceCareBundleCheckRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.DeviceAssignmentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and deviceAssignmentId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Submit for deviceAssignmentId: {DeviceAssignmentId}", request.DeviceAssignmentId);
                return StatusCode(500, new { Message = "An error occurred while recording the bundle check." });
            }
        }
    }
}
