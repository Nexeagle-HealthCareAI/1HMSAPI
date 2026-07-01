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
    [Route("medication-order")]
    [Authorize]
    public class MedicationOrderController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<MedicationOrderController> _logger;

        public MedicationOrderController(IMediator mediator, ILogger<MedicationOrderController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Every medication order for an admission, newest first.
        [HttpGet]
        public async Task<ActionResult<GetMedicationOrdersResponseModel>> GetMedicationOrders([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var request = new GetMedicationOrdersRequestModel { HospitalId = hospitalId, AdmissionId = admissionId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMedicationOrders for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching medication orders." });
            }
        }

        // Places a medication order (one or more drugs); chargeable lines are billed immediately.
        [HttpPost]
        public async Task<ActionResult<PlaceMedicationOrderResponseModel>> PlaceMedicationOrder([FromBody] PlaceMedicationOrderRequestModel request)
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
                _logger.LogError(ex, "Error in PlaceMedicationOrder for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while placing the medication order." });
            }
        }

        // Discontinues one line of a medication order; voids its charge event too, if any.
        [HttpPost("discontinue-line")]
        public async Task<ActionResult<DiscontinueMedicationOrderLineResponseModel>> DiscontinueLine([FromBody] DiscontinueMedicationOrderLineRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.OrderLineId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and orderLineId are required." });

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
                _logger.LogError(ex, "Error in DiscontinueLine for orderLineId: {OrderLineId}", request.OrderLineId);
                return StatusCode(500, new { Message = "An error occurred while discontinuing the order line." });
            }
        }
    }
}
