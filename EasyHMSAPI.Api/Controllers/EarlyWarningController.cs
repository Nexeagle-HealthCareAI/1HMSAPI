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
    // Early Warning Score (NEWS2-style) — deliberately a general "vitals" route, not under /icu,
    // since this scores any IPD admission (ward or ICU): a deteriorating ward patient should be
    // flagged before a crisis, not only tracked once they reach ICU.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("vitals/ews")]
    [Authorize]
    public class EarlyWarningController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<EarlyWarningController> _logger;

        public EarlyWarningController(IMediator mediator, ILogger<EarlyWarningController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("autofill")]
        public async Task<ActionResult<GetEarlyWarningAutoFillResponseModel>> GetAutoFill([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetEarlyWarningAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAutoFill for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while composing the Early Warning Score auto-fill." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<RecordEarlyWarningScoreResponseModel>> RecordScore([FromBody] RecordEarlyWarningScoreRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in RecordScore for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the Early Warning Score." });
            }
        }

        [HttpGet("history")]
        public async Task<ActionResult<GetEarlyWarningScoreHistoryResponseModel>> GetHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetEarlyWarningScoreHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching Early Warning Score history." });
            }
        }
    }
}
