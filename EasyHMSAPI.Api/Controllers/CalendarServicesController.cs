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
    [Route("calendar")]
    [ApiController]
    public class CalendarServicesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CalendarServicesController> _logger;
        public CalendarServicesController(IMediator mediator, ILogger<CalendarServicesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("doctor/config")]
        [Authorize]
        public async Task<ActionResult<DoctorShiftConfigResponseModel>> GetDoctorShiftConfig([FromQuery] Guid doctorId, [FromQuery] DateTime? startDate,[FromQuery] int? daysCount)
        {
            _logger.LogInformation("GetDoctorShiftConfig started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, doctorId);
            try
            {
                if (doctorId == Guid.Empty)
                {
                    return BadRequest(new { Message = "doctorId is required" });
                }
                if (!startDate.HasValue)
                {
                    return BadRequest(new { Message = "start is required" });
                }
                if (!daysCount.HasValue || daysCount.Value <= 0)
                {
                    return BadRequest(new { Message = "days is required" });
                }

                DoctorShiftConfigRequestModel request = new()
                {
                    DoctorId = doctorId,
                    StartDate = startDate.Value.Date,
                    DaysCount = daysCount
                };

                var response = await _mediator.Send(request);
                if (response == null)
                    return NotFound(new { Message = "No config found" });
                _logger.LogInformation("GetDoctorShiftConfig ended for doctorId: {DoctorId}", doctorId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetDoctorShiftConfig for doctorId: {DoctorId}. Error: {Error}", doctorId, ex);
                return StatusCode(500, new { Message = "An error occurred while processing the request.", Details = ex.Message });
            }
        }

        [HttpPost("doctor/override")]
        [Authorize]
        public async Task<ActionResult<DoctorOverrideCreateResponseModel>> CreateDoctorOverride([FromBody] DoctorOverrideCreateRequestModel request)
        {
            _logger.LogInformation("CreateDoctorOverride started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, request.DoctorId);
            try
            {
                if (request.DoctorId == Guid.Empty)
                {
                    return BadRequest(new { Message = "doctorId is required" });
                }
                var response = await _mediator.Send(request);
                if (response.Success) 
                { 
                    _logger.LogInformation("CreateDoctorOverride successful for doctorId: {DoctorId}", request.DoctorId);
                    return Ok(response); 
                }
                _logger.LogWarning("CreateDoctorOverride failed for doctorId: {DoctorId}", request.DoctorId);
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in CreateDoctorOverride for doctorId: {DoctorId}. Error: {Error}", request.DoctorId, ex);
                return StatusCode(500, new { Message = "An error occurred while processing the request.", Details = ex.Message });
            }
        }

        [HttpDelete("doctor/override/{overrideId}")]
        [Authorize]
        public async Task<ActionResult<DoctorOverrideDeleteResponseModel>> DeleteDoctorOverride([FromRoute] Guid overrideId)
        {
            _logger.LogInformation("DeleteDoctorOverride called for overrideId: {OverrideId} at {Time}", overrideId, DateTime.UtcNow);
            if (overrideId == Guid.Empty) return BadRequest(new { Message = "overrideId is required" });
            var request = new DoctorOverrideDeleteRequestModel { OverrideId = overrideId };
            var response = await _mediator.Send(request);
            if (response.Success) 
            { 
                _logger.LogInformation("DeleteDoctorOverride successful for overrideId: {OverrideId}", overrideId);
                return Ok(response); 
            }
            _logger.LogWarning("DeleteDoctorOverride not found for overrideId: {OverrideId}", overrideId);
            return NotFound(response);
        }

        [HttpPost("doctor/timeoff")]
        [Authorize]
        public async Task<ActionResult<DoctorTimeOffCreateResponseModel>> CreateDoctorTimeOff([FromBody] DoctorTimeOffCreateRequestModel request)
        {
            _logger.LogInformation("CreateDoctorTimeOff called at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, request.DoctorId);
            if (request.DoctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });
            var response = await _mediator.Send(request);
            if (response.Success) 
            { 
                _logger.LogInformation("CreateDoctorTimeOff successful for doctorId: {DoctorId}", request.DoctorId);
                return Ok(response); 
            }
            _logger.LogWarning("CreateDoctorTimeOff failed for doctorId: {DoctorId}", request.DoctorId);
            return BadRequest(response);
        }

        [HttpGet("doctor/timeoff")]
        [Authorize]
        public async Task<ActionResult<DoctorTimeOffListResponseModel>> GetDoctorTimeOff([FromQuery] Guid doctorId)
        {
            _logger.LogInformation("GetDoctorTimeOff called at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, doctorId);
            if (doctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });
            var request = new DoctorTimeOffListRequestModel { DoctorId = doctorId };
            var response = await _mediator.Send(request);
            return Ok(response);
        }

        [HttpDelete("doctor/timeoff/{timeOffId}")]
        [Authorize]
        public async Task<ActionResult<DoctorTimeOffDeleteResponseModel>> DeleteDoctorTimeOff([FromRoute] Guid timeOffId)
        {
            _logger.LogInformation("DeleteDoctorTimeOff called for timeOffId: {TimeOffId} at {Time}", timeOffId, DateTime.UtcNow);
            if (timeOffId == Guid.Empty) return BadRequest(new { Message = "timeOffId is required" });
            var request = new DoctorTimeOffDeleteRequestModel { TimeOffId = timeOffId };
            var response = await _mediator.Send(request);
            if (response.Success) 
            { 
                _logger.LogInformation("DeleteDoctorTimeOff successful for timeOffId: {TimeOffId}", timeOffId);
                return Ok(response); 
            }
            _logger.LogWarning("DeleteDoctorTimeOff not found for timeOffId: {TimeOffId}", timeOffId);
            return NotFound(response);
        }

        [HttpGet("doctor/slots")]
        [Authorize]
        public async Task<ActionResult<DoctorSlotsResponseModel>> GetDoctorSlots([FromQuery] Guid doctorId, [FromQuery] DateTime slotDate)
        {
            _logger.LogInformation("GetDoctorSlots called at {Time} for doctorId: {DoctorId} on date: {SlotDate}", DateTime.UtcNow, doctorId, slotDate);
            if (doctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });

            var request = new DoctorSlotsRequestModel { DoctorId = doctorId, SlotDate = slotDate };
            var response = await _mediator.Send(request);
            return Ok(response);
        }
    }
}
