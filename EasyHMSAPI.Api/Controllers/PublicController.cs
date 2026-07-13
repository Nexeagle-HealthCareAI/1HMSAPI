using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Public, API-key-gated surface for external integrations (the Nexeagle booking website).
    /// No staff JWT — PublicApiKeyFilter resolves HospitalId from the X-Api-Key header and stamps
    /// it onto HttpContext.Items; every action here pulls it from there, never from the request
    /// body/query, so a caller can never reach another hospital's data by supplying a different id.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public")]
    [AllowAnonymous]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    [ServiceFilter(typeof(PublicApiKeyFilter))]
    [EnableRateLimiting("PublicBookingPolicy")]
    public class PublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PublicController> _logger;

        public PublicController(IMediator mediator, ILogger<PublicController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private Guid ResolveHospitalId() => (Guid)HttpContext.Items[PublicApiKeyFilter.HospitalIdItemKey]!;

        [HttpGet("doctors")]
        public async Task<ActionResult<GetPublicDoctorsResponseModel>> GetDoctors()
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorsRequestModel { HospitalId = ResolveHospitalId() });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctors");
                return StatusCode(500, new { Message = "An error occurred while fetching doctors." });
            }
        }

        [HttpGet("doctors/{doctorId:guid}/availability")]
        public async Task<ActionResult<GetPublicDoctorAvailabilityResponseModel>> GetDoctorAvailability(Guid doctorId, [FromQuery] DateTime date)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorAvailabilityRequestModel
                {
                    HospitalId = ResolveHospitalId(),
                    DoctorId = doctorId,
                    Date = date,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorAvailability for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching availability." });
            }
        }

        [HttpPost("appointments")]
        public async Task<ActionResult<PublicBookAppointmentResponseModel>> BookAppointment([FromBody] PublicBookAppointmentRequestModel request)
        {
            try
            {
                request.HospitalId = ResolveHospitalId();
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.BookAppointment");
                return StatusCode(500, new { Message = "An error occurred while booking the appointment." });
            }
        }
    }
}
