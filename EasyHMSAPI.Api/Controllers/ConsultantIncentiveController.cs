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
    // Consultant (treating-doctor) incentive sub-ledger — billing/finance concern, kept separate
    // from ChargeController's charge-posting concern, matches the AdmissionController/ChargeController split.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("consultant-incentive")]
    [Authorize]
    public class ConsultantIncentiveController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ConsultantIncentiveController> _logger;

        public ConsultantIncentiveController(IMediator mediator, ILogger<ConsultantIncentiveController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<GetConsultantIncentiveSummaryResponseModel>> GetSummary([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetConsultantIncentiveSummaryRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSummary for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while loading the consultant incentive summary." });
            }
        }

        [HttpGet("ledger")]
        public async Task<ActionResult<GetConsultantIncentiveLedgerResponseModel>> GetLedger(
            [FromQuery] Guid hospitalId, [FromQuery] Guid doctorId, [FromQuery] string? statusCode,
            [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            if (hospitalId == Guid.Empty || doctorId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and doctorId are required." });

            try
            {
                var response = await _mediator.Send(new GetConsultantIncentiveLedgerRequestModel
                {
                    HospitalId = hospitalId,
                    DoctorId = doctorId,
                    StatusCode = statusCode,
                    FromDate = fromDate,
                    ToDate = toDate,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLedger for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while loading the consultant incentive ledger." });
            }
        }

        [HttpPost("settle")]
        public async Task<ActionResult<SettleConsultantIncentivesResponseModel>> Settle([FromBody] SettleConsultantIncentivesRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.DoctorId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and doctorId are required." });

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
                _logger.LogError(ex, "Error in Settle for doctorId: {DoctorId}", request.DoctorId);
                return StatusCode(500, new { Message = "An error occurred while settling the incentives." });
            }
        }
    }
}
