using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>1HMS portal, Settings -> Hospital Info -> Patient visit guide (Vita vision, Phase 4). Members read; admins save.</summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("hospitals/{hospitalId:guid}/visit-guide")]
    [Authorize]
    public class VisitGuideController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<VisitGuideController> _logger;

        public VisitGuideController(IMediator mediator, ILogger<VisitGuideController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<HospitalVisitGuideResponseModel>> Get(Guid hospitalId)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var r = await _mediator.Send(new GetHospitalVisitGuideRequestModel { HospitalId = hospitalId, CallerUserId = userId.Value });
                return r.Success ? Ok(r) : (ActionResult)StatusCode(403, r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the visit guide for {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while reading the visit guide." });
            }
        }

        [HttpPut]
        public async Task<ActionResult<HospitalVisitGuideResponseModel>> Save(Guid hospitalId, [FromBody] SaveHospitalVisitGuideRequestModel request)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            request.HospitalId = hospitalId;
            request.CallerUserId = userId.Value;
            try
            {
                var r = await _mediator.Send(request);
                return r.Success ? Ok(r) : (ActionResult)BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving the visit guide for {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the visit guide." });
            }
        }
    }

    /// <summary>The patient's visit page (Doctor Dekho, Vita, the reminders' "Visit details" button). Same trust model as the guest
    /// booking lookup: the appointment id is the only gate, and the answer holds nothing personal.</summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(PublicApiKeyFilter))]
    [EnableRateLimiting("PublicBookingPolicy")]
    public class PublicVisitController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PublicVisitController> _logger;

        public PublicVisitController(IMediator mediator, ILogger<PublicVisitController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("appointments/{appointmentId:guid}/visit")]
        public async Task<ActionResult<PublicVisitResponseModel>> Visit(Guid appointmentId, CancellationToken ct)
        {
            try
            {
                var r = await _mediator.Send(new GetPublicVisitRequestModel { AppointmentId = appointmentId }, ct);
                return r.Success ? Ok(r) : (ActionResult)NotFound(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the visit for {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while reading the visit." });
            }
        }

        /// <summary>Vita "takes you there": how the patient is coming, when they leave, and whether reception may see their ETA.</summary>
        [HttpPut("appointments/{appointmentId:guid}/journey")]
        public async Task<ActionResult<PublicJourneyResponseModel>> SaveJourney(Guid appointmentId, [FromBody] SavePublicJourneyRequestModel request, CancellationToken ct)
        {
            request.AppointmentId = appointmentId;
            request.NowUtc = DateTime.UtcNow;
            return await Journey(() => _mediator.Send(request, ct), appointmentId);
        }

        /// <summary>On the way (ETA minutes, distance left), late, arrived or cancelled. No location is sent or kept.</summary>
        [HttpPost("appointments/{appointmentId:guid}/journey/progress")]
        public async Task<ActionResult<PublicJourneyResponseModel>> JourneyProgress(Guid appointmentId, [FromBody] UpdatePublicJourneyRequestModel request, CancellationToken ct)
        {
            request.AppointmentId = appointmentId;
            request.NowUtc = DateTime.UtcNow;
            return await Journey(() => _mediator.Send(request, ct), appointmentId);
        }

        private async Task<ActionResult<PublicJourneyResponseModel>> Journey(Func<Task<PublicJourneyResponseModel>> run, Guid appointmentId)
        {
            try
            {
                var r = await run();
                if (r.Success) return Ok(r);
                return r.Message == "Appointment not found." ? NotFound(r) : BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating the journey for {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while saving the journey." });
            }
        }
    }
}
