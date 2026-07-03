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
    // OT theatre resource + booking calendar. Separate from SurgeryCaseController — scheduling is
    // a resource-booking concern, not clinical documentation.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("ot-booking")]
    [Authorize]
    public class OTBookingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OTBookingController> _logger;

        public OTBookingController(IMediator mediator, ILogger<OTBookingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("theatres")]
        public async Task<ActionResult<GetOperationTheatresResponseModel>> GetTheatres([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetOperationTheatresRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTheatres for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching theatres." });
            }
        }

        [HttpPost("theatre")]
        public async Task<ActionResult<CreateOperationTheatreResponseModel>> CreateTheatre([FromBody] CreateOperationTheatreRequestModel request)
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
                _logger.LogError(ex, "Error in CreateTheatre for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the theatre." });
            }
        }

        [HttpGet("schedule")]
        public async Task<ActionResult<GetOTScheduleResponseModel>> GetSchedule([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetOTScheduleRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSchedule for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the OT schedule." });
            }
        }

        [HttpPost("book")]
        public async Task<ActionResult<CreateOTBookingResponseModel>> Book([FromBody] CreateOTBookingRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty || request.TheatreId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, surgeryCaseId, and theatreId are required." });

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
                _logger.LogError(ex, "Error in Book for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the booking." });
            }
        }

        [HttpPost("reschedule")]
        public async Task<ActionResult<RescheduleOTBookingResponseModel>> Reschedule([FromBody] RescheduleOTBookingRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.OTBookingId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and otBookingId are required." });

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
                _logger.LogError(ex, "Error in Reschedule for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while rescheduling the booking." });
            }
        }

        [HttpPost("cancel")]
        public async Task<ActionResult<CancelOTBookingResponseModel>> Cancel([FromBody] CancelOTBookingRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.OTBookingId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and otBookingId are required." });

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
                _logger.LogError(ex, "Error in Cancel for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while cancelling the booking." });
            }
        }
    }
}
