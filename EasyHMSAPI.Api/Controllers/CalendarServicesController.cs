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
        public async Task<ActionResult<DoctorShiftConfigResponseModel>> GetDoctorShiftConfig([FromQuery] Guid doctorId, [FromQuery] Guid hospitalId, [FromQuery] DateTime? startDate,[FromQuery] int? daysCount)
        {
            _logger.LogInformation("GetDoctorShiftConfig started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            try
            {
                if (doctorId == Guid.Empty)
                {
                    return BadRequest(new { Message = "doctorId is required" });
                }
                if (hospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "hospitalId is required" });
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
                    HospitalId = hospitalId,
                    StartDate = startDate.Value.Date,
                    DaysCount = daysCount
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("GetDoctorShiftConfig ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in GetDoctorShiftConfig for doctorId: {DoctorId}, hospitalId: {HospitalId}. Error: {Error}", doctorId, hospitalId, ex);
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
                _logger.LogInformation("CreateDoctorOverride successful for doctorId: {DoctorId}", request.DoctorId);

                return Ok(response);
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
            _logger.LogInformation("DeleteDoctorOverride successful for overrideId: {OverrideId}", overrideId);

            return Ok(response); 
        }

        [HttpPost("doctor/timeoff")]
        [Authorize]
        public async Task<ActionResult<DoctorTimeOffCreateResponseModel>> CreateDoctorTimeOff([FromBody] DoctorTimeOffCreateRequestModel request)
        {
            _logger.LogInformation("CreateDoctorTimeOff called at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, request.DoctorId);
            if (request.DoctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });
            var response = await _mediator.Send(request);
            _logger.LogInformation("CreateDoctorTimeOff successful for doctorId: {DoctorId}", request.DoctorId);

            return Ok(response); 
        }

        [HttpGet("doctor/timeoff")]
        [Authorize]
        public async Task<ActionResult<DoctorTimeOffListResponseModel>> GetDoctorTimeOff([FromQuery] Guid doctorId, [FromQuery] Guid hospitalId)
        {
            _logger.LogInformation("GetDoctorTimeOff called at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            if (doctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required" });
            var request = new DoctorTimeOffListRequestModel { DoctorId = doctorId, HospitalId = hospitalId };
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
            _logger.LogInformation("DeleteDoctorTimeOff successful for timeOffId: {TimeOffId}", timeOffId);

            return Ok(response); 
        }

        [HttpGet("doctor/slots")]
        [Authorize]
        public async Task<ActionResult<DoctorSlotsResponseModel>> GetDoctorSlots([FromQuery] Guid doctorId, [FromQuery] Guid hospitalId, [FromQuery] DateTime slotDate)
        {
            _logger.LogInformation("GetDoctorSlots called at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId} on date: {SlotDate}", DateTime.UtcNow, doctorId, hospitalId, slotDate);
            if (doctorId == Guid.Empty) return BadRequest(new { Message = "doctorId is required" });
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required" });

            var request = new DoctorSlotsRequestModel { DoctorId = doctorId, HospitalId = hospitalId, SlotDate = slotDate };
            var response = await _mediator.Send(request);

            return Ok(response);
        }

        [HttpGet("roster")]
        [Authorize]
        public async Task<ActionResult<GetDoctorAvailabilityRosterResponseModel>> GetDoctorAvailabilityRoster([FromQuery] Guid hospitalId, [FromQuery] DateTime? date)
        {
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required" });

            var request = new GetDoctorAvailabilityRosterRequestModel { HospitalId = hospitalId, Date = date };
            var response = await _mediator.Send(request);

            return Ok(response);
        }
    }
}
