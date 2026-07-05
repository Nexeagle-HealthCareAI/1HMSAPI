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
    // Biomedical/ICT/facility asset register — wires the previously-unused Equipment/MaintenanceLog
    // tables (calibration/AMC/PM scheduling, downtime-repair history).
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("equipment")]
    [Authorize]
    public class EquipmentController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<EquipmentController> _logger;

        public EquipmentController(IMediator mediator, ILogger<EquipmentController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetEquipmentListResponseModel>> GetEquipment(
            [FromQuery] Guid hospitalId, [FromQuery] string? status, [FromQuery] string? department,
            [FromQuery] string? category, [FromQuery] bool dueOnly = false)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetEquipmentListRequestModel
                {
                    HospitalId = hospitalId,
                    Status = status,
                    Department = department,
                    Category = category,
                    DueOnly = dueOnly,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetEquipment for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching equipment." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<UpsertEquipmentResponseModel>> UpsertEquipment([FromBody] UpsertEquipmentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertEquipment for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the asset." });
            }
        }

        [HttpGet("{equipmentId:guid}/maintenance-log")]
        public async Task<ActionResult<GetMaintenanceLogHistoryResponseModel>> GetMaintenanceLogHistory(Guid equipmentId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || equipmentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and equipmentId are required." });

            try
            {
                var response = await _mediator.Send(new GetMaintenanceLogHistoryRequestModel { HospitalId = hospitalId, EquipmentId = equipmentId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMaintenanceLogHistory for equipmentId: {EquipmentId}", equipmentId);
                return StatusCode(500, new { Message = "An error occurred while fetching maintenance history." });
            }
        }

        [HttpPost("{equipmentId:guid}/maintenance-log")]
        public async Task<ActionResult<RecordMaintenanceLogResponseModel>> RecordMaintenanceLog(Guid equipmentId, [FromBody] RecordMaintenanceLogRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.EquipmentId = equipmentId;

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
                _logger.LogError(ex, "Error in RecordMaintenanceLog for equipmentId: {EquipmentId}", equipmentId);
                return StatusCode(500, new { Message = "An error occurred while recording the maintenance log." });
            }
        }
    }
}
