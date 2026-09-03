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
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("alerts")]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(IMediator mediator, ILogger<AlertsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetAlertsResponseModel>> GetAlerts(
            [FromQuery] Guid hospitalId,
            [FromQuery] string? status,
            [FromQuery] string? severity,
            [FromQuery] string? alertCode,
            [FromQuery] Guid? admissionId,
            [FromQuery] Guid? audienceUserId,
            [FromQuery] string? role,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int? take)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetAlertsRequestModel
                {
                    HospitalId = hospitalId,
                    Status = status,
                    Severity = severity,
                    AlertCode = alertCode,
                    AdmissionId = admissionId,
                    AudienceUserId = audienceUserId,
                    Role = role,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    Take = take,
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAlerts for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching alerts." });
            }
        }

        [HttpGet("counts")]
        public async Task<ActionResult<GetAlertCountsResponseModel>> GetAlertCounts(
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid? audienceUserId,
            [FromQuery] string? role)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetAlertCountsRequestModel { HospitalId = hospitalId, AudienceUserId = audienceUserId, Role = role };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAlertCounts for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching alert counts." });
            }
        }

        [HttpPost("raise")]
        public async Task<ActionResult<RaiseAlertResponseModel>> RaiseAlert([FromBody] RaiseAlertRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                await PopulateActor(request);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RaiseAlert for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while raising the alert." });
            }
        }

        [HttpPost("acknowledge")]
        public async Task<ActionResult<AlertActionResponseModel>> AcknowledgeAlert([FromBody] AcknowledgeAlertRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AlertId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and alertId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AcknowledgeAlert for alertId: {AlertId}", request.AlertId);
                return StatusCode(500, new { Message = "An error occurred while acknowledging the alert." });
            }
        }

        [HttpPost("dismiss")]
        public async Task<ActionResult<AlertActionResponseModel>> DismissAlert([FromBody] DismissAlertRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AlertId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and alertId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DismissAlert for alertId: {AlertId}", request.AlertId);
                return StatusCode(500, new { Message = "An error occurred while dismissing the alert." });
            }
        }

        [HttpPost("snooze")]
        public async Task<ActionResult<AlertActionResponseModel>> SnoozeAlert([FromBody] SnoozeAlertRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AlertId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and alertId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SnoozeAlert for alertId: {AlertId}", request.AlertId);
                return StatusCode(500, new { Message = "An error occurred while snoozing the alert." });
            }
        }

        [HttpPost("evaluate")]
        public async Task<ActionResult<EvaluateAlertsResponseModel>> EvaluateAlerts([FromBody] EvaluateAlertsRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EvaluateAlerts for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while evaluating alerts." });
            }
        }

        [HttpPost("evaluate-expiry")]
        public async Task<ActionResult<EvaluateExpiryAlertsResponseModel>> EvaluateExpiryAlerts([FromBody] EvaluateExpiryAlertsRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EvaluateExpiryAlerts for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while evaluating expiry alerts." });
            }
        }

        private async Task PopulateActor(RaiseAlertRequestModel request)
        {
            request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
            request.LoggedInUserId = UserContextHelper.GetUserId(User);
        }
    }
}
