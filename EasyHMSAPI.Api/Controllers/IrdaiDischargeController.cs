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
    // TPA payable/non-payable split + IRDAI discharge-process clocks. Billing/TPA-process
    // concern, kept separate from DischargeSummaryController's clinical-documentation concern —
    // matches the existing AdmissionController/ChargeController split.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("irdai-discharge")]
    [Authorize]
    public class IrdaiDischargeController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<IrdaiDischargeController> _logger;

        public IrdaiDischargeController(IMediator mediator, ILogger<IrdaiDischargeController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("tpa-split")]
        public async Task<ActionResult<GetDischargeTpaSplitResponseModel>> GetTpaSplit([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetDischargeTpaSplitRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTpaSplit for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while computing the TPA split." });
            }
        }

        [HttpGet("clocks")]
        public async Task<ActionResult<GetIrdaiDischargeClocksResponseModel>> GetClocks([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetIrdaiDischargeClocksRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetClocks for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading the IRDAI discharge clocks." });
            }
        }

        [HttpPost("stamp-milestone")]
        public async Task<ActionResult<StampIrdaiMilestoneResponseModel>> StampMilestone([FromBody] StampIrdaiMilestoneRequestModel request)
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
                _logger.LogError(ex, "Error in StampMilestone for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while recording the milestone." });
            }
        }
    }
}
