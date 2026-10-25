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
    }
}
