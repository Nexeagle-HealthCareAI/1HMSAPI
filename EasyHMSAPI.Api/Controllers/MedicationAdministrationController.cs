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
    // MAR — nurse-side dose recording against MEDICATION CPOE order lines.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("mar")]
    [Authorize]
    public class MedicationAdministrationController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<MedicationAdministrationController> _logger;

        public MedicationAdministrationController(IMediator mediator, ILogger<MedicationAdministrationController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // MAR grid for one admission, one IST calendar day.
        [HttpGet("grid")]
        public async Task<ActionResult<GetMarGridResponseModel>> GetGrid([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId, [FromQuery] DateTime dayStartUtc)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetMarGridRequestModel { HospitalId = hospitalId, AdmissionId = admissionId, DayStartUtc = dayStartUtc });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetGrid for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading the MAR grid." });
            }
        }

        // Records one nurse action against a computed dose slot.
        [HttpPost("record")]
        public async Task<ActionResult<RecordMedicationAdministrationResponseModel>> Record([FromBody] RecordMedicationAdministrationRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.OrderLineId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and orderLineId are required." });

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
                _logger.LogError(ex, "Error in Record for orderLineId: {OrderLineId}", request.OrderLineId);
                return StatusCode(500, new { Message = "An error occurred while recording the administration." });
            }
        }
    }
}
