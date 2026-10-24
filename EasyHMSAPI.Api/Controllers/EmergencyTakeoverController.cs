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
    /// <summary>
    /// 1HMS portal, Settings -> Emergency takeover (Vita vision, Phase 3): a hospital opts in to be shown to patients with a possible
    /// emergency nearby and to receive "possible emergency nearby" alerts. Any member can read; only a hospital admin can save.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("hospitals/{hospitalId:guid}/emergency-settings")]
    [Authorize]
    public class EmergencyTakeoverController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<EmergencyTakeoverController> _logger;

        public EmergencyTakeoverController(IMediator mediator, ILogger<EmergencyTakeoverController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<HospitalEmergencySettingsResponseModel>> Get(Guid hospitalId)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var r = await _mediator.Send(new GetHospitalEmergencySettingsRequestModel { HospitalId = hospitalId, CallerUserId = userId.Value });
                return r.Success ? Ok(r) : (ActionResult)StatusCode(403, r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading emergency settings for {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while reading the emergency settings." });
            }
        }

        [HttpPut]
        public async Task<ActionResult<HospitalEmergencySettingsResponseModel>> Save(Guid hospitalId, [FromBody] SaveHospitalEmergencySettingsRequestModel request)
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
                _logger.LogError(ex, "Error saving emergency settings for {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the emergency settings." });
            }
        }
    }

    /// <summary>
    /// Emergency takeover for patients (Vita in Doctor Dekho, the NLP router, the WhatsApp bot). Listing the nearest places is open like the
    /// rest of /public. Alerting hospitals sends WhatsApp messages, so it needs a valid X-Api-Key (an identified, revocable caller) and has
    /// its own tighter limit; each chat session alerts once per emergency.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(PublicApiKeyFilter))]
    [EnableRateLimiting("PublicBookingPolicy")]
    public class PublicEmergencyController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PublicEmergencyController> _logger;

        public PublicEmergencyController(IMediator mediator, ILogger<PublicEmergencyController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private bool IdentifiedCaller => HttpContext.Items.ContainsKey(PublicApiKeyFilter.ClientItemKey);

        /// <summary>Nearest emergency-ready places, open now, nearest first (at most 5). capability: cardiac, maternity, paediatric, ...</summary>
        [HttpGet("emergency-places")]
        public async Task<ActionResult<EmergencyPlacesResponseModel>> Places([FromQuery] double lat, [FromQuery] double lng, [FromQuery] string? capability, [FromQuery] int take = 5)
        {
            try
            {
                var r = await _mediator.Send(new GetEmergencyPlacesRequestModel { Latitude = lat, Longitude = lng, Capability = capability, Take = take });
                return r.Success ? Ok(r) : (ActionResult)BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing emergency places");
                return StatusCode(500, new { Message = "An error occurred while finding emergency help." });
            }
        }

        [HttpPost("emergency-alerts")]
        [EnableRateLimiting("EmergencyAlertPolicy")]
        public async Task<ActionResult<RaiseEmergencyAlertResponseModel>> Raise([FromBody] RaiseEmergencyAlertRequestModel request)
        {
            if (!IdentifiedCaller) return StatusCode(401, new { Message = "An API key is needed to alert hospitals." });
            try
            {
                var r = await _mediator.Send(request);
                return r.Success ? Ok(r) : (ActionResult)BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising an emergency alert");
                return StatusCode(500, new { Message = "An error occurred while alerting hospitals." });
            }
        }

        [HttpPost("emergency-alerts/{eventId:guid}/share-number")]
        [EnableRateLimiting("EmergencyAlertPolicy")]
        public async Task<ActionResult<EmergencyActionResponseModel>> ShareNumber(Guid eventId, [FromBody] ShareEmergencyNumberRequestModel request)
        {
            if (!IdentifiedCaller) return StatusCode(401, new { Message = "An API key is needed to contact hospitals." });
            request.EventId = eventId;
            try
            {
                var r = await _mediator.Send(request);
                return r.Success ? Ok(r) : (ActionResult)BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing a number for emergency {EventId}", eventId);
                return StatusCode(500, new { Message = "An error occurred while sharing the number." });
            }
        }

        [HttpPost("emergency-alerts/{eventId:guid}/call")]
        public async Task<ActionResult<EmergencyActionResponseModel>> Call(Guid eventId, [FromBody] LogEmergencyCallRequestModel request)
        {
            request.EventId = eventId;
            try
            {
                var r = await _mediator.Send(request);
                return r.Success ? Ok(r) : (ActionResult)NotFound(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging an emergency call tap for {EventId}", eventId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }

    /// <summary>
    /// Emergency takeover for the CMS API (server-to-server, shared key; the CMS user in X-Actor): verify or reject hospital opt-ins, add
    /// places that are not on 1HMS (district hospitals, ambulance services), and read the alert log.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("internal/emergency")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(InternalApiKeyFilter))]
    [EnableRateLimiting("PerIpPolicy")]
    public class EmergencyInternalController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<EmergencyInternalController> _logger;

        public EmergencyInternalController(IMediator mediator, ILogger<EmergencyInternalController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private string Actor() => Request.Headers["X-Actor"].ToString();

        [HttpGet("places")]
        public async Task<ActionResult<EmergencyPlaceListResponseModel>> Places([FromQuery] string? status)
            => await Run(() => _mediator.Send(new ListEmergencyPlacesRequestModel { Status = status }), "listing emergency places");

        [HttpPost("places/{settingId:guid}/verify")]
        public async Task<ActionResult<EmergencyActionResponseModel>> Verify(Guid settingId, [FromBody] VerifyEmergencyPlaceRequestModel request)
        {
            request.SettingId = settingId;
            request.Actor = Actor();
            return await Run(() => _mediator.Send(request), "verifying an emergency place", r => r.Success);
        }

        [HttpPost("places")]
        public async Task<ActionResult<EmergencyPlaceListResponseModel>> Add([FromBody] SaveCmsEmergencyPlaceRequestModel request)
        {
            request.SettingId = null;
            request.Actor = Actor();
            return await Run(() => _mediator.Send(request), "adding an emergency place", r => r.Success);
        }

        [HttpPut("places/{settingId:guid}")]
        public async Task<ActionResult<EmergencyPlaceListResponseModel>> Update(Guid settingId, [FromBody] SaveCmsEmergencyPlaceRequestModel request)
        {
            request.SettingId = settingId;
            request.Actor = Actor();
            return await Run(() => _mediator.Send(request), "changing an emergency place", r => r.Success);
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<EmergencyAlertLogResponseModel>> Alerts([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int take = 100)
            => await Run(() => _mediator.Send(new GetEmergencyAlertLogRequestModel { FromUtc = fromUtc, ToUtc = toUtc, Take = take }), "reading the emergency alert log");

        private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action, string what, Func<T, bool>? ok = null)
        {
            try
            {
                var r = await action();
                return ok == null || ok(r) ? Ok(r) : (ActionResult)BadRequest(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {What}", what);
                return StatusCode(500, new { Message = $"An error occurred while {what}." });
            }
        }
    }
}
