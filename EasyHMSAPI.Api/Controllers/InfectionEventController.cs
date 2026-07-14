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
    // Device-associated (and other) infection event logging + hospital-wide rate summary.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("infection-events")]
    [Authorize]
    public class InfectionEventController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<InfectionEventController> _logger;

        public InfectionEventController(IMediator mediator, ILogger<InfectionEventController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetInfectionEventsResponseModel>> GetEvents([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetInfectionEventsRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetEvents for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading infection events." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<LogInfectionEventResponseModel>> Log([FromBody] LogInfectionEventRequestModel request)
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
                _logger.LogError(ex, "Error in Log for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while logging the infection event." });
            }
        }

        [HttpGet("rate-summary")]
        public async Task<ActionResult<GetInfectionRateSummaryResponseModel>> GetRateSummary([FromQuery] Guid hospitalId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetInfectionRateSummaryRequestModel { HospitalId = hospitalId, FromDate = fromDate, ToDate = toDate });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRateSummary for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing the infection rate summary." });
            }
        }
    }
}
