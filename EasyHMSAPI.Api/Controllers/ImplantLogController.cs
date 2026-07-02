using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    // Implant recall/traceability search over IntraOpItemUsage — a billing/traceability
    // cross-cutting concern, kept separate from the clinical SurgeryCaseController.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("implant-log")]
    [Authorize]
    public class ImplantLogController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ImplantLogController> _logger;

        public ImplantLogController(IMediator mediator, ILogger<ImplantLogController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<ActionResult<GetImplantLogResponseModel>> Search(
            [FromQuery] Guid hospitalId, [FromQuery] string? lotNumber, [FromQuery] string? serialNumber, [FromQuery] Guid? admissionId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetImplantLogRequestModel
                {
                    HospitalId = hospitalId,
                    LotNumber = lotNumber,
                    SerialNumber = serialNumber,
                    AdmissionId = admissionId,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Search for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while searching the implant log." });
            }
        }
    }
}
