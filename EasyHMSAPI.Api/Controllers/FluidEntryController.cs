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
    // Intake/output balance charting.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("fluid-entry")]
    [Authorize]
    public class FluidEntryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<FluidEntryController> _logger;

        public FluidEntryController(IMediator mediator, ILogger<FluidEntryController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("balance")]
        public async Task<ActionResult<GetFluidBalanceResponseModel>> GetBalance(
            [FromQuery] Guid hospitalId, [FromQuery] Guid admissionId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetFluidBalanceRequestModel { HospitalId = hospitalId, AdmissionId = admissionId, FromUtc = fromUtc, ToUtc = toUtc });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBalance for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading the fluid balance." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<RecordFluidEntryResponseModel>> Record([FromBody] RecordFluidEntryRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Record for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while recording the fluid entry." });
            }
        }
    }
}
