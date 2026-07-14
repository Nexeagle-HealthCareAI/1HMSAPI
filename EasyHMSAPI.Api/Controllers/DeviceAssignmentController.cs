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
    // Invasive device tracking (central line/catheter/ETT) driving CLABSI/CAUTI/VAP risk.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("devices")]
    [Authorize]
    public class DeviceAssignmentController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DeviceAssignmentController> _logger;

        public DeviceAssignmentController(IMediator mediator, ILogger<DeviceAssignmentController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetDeviceAssignmentsResponseModel>> GetDevices([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetDeviceAssignmentsRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDevices for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading devices." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<InsertDeviceResponseModel>> Insert([FromBody] InsertDeviceRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Insert for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while inserting the device." });
            }
        }

        [HttpPost("remove")]
        public async Task<ActionResult<RemoveDeviceResponseModel>> Remove([FromBody] RemoveDeviceRequestModel request)
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
                _logger.LogError(ex, "Error in Remove for deviceAssignmentId: {DeviceAssignmentId}", request.DeviceAssignmentId);
                return StatusCode(500, new { Message = "An error occurred while removing the device." });
            }
        }
    }
}
