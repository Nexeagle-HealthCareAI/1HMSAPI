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
    // CPOE orders — one controller for every OrderType (Medication/Lab/Radiology/Procedure/
    // Diet/Nursing); callers pass orderType to scope each request to one tab's worth of orders.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("clinical-order")]
    [Authorize]
    public class ClinicalOrderController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ClinicalOrderController> _logger;

        public ClinicalOrderController(IMediator mediator, ILogger<ClinicalOrderController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Every order of one OrderType for an admission, newest first.
        [HttpGet]
        public async Task<ActionResult<GetClinicalOrdersResponseModel>> GetOrders([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId, [FromQuery] string orderType)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty || string.IsNullOrWhiteSpace(orderType))
                return BadRequest(new { Message = "hospitalId, admissionId and orderType are required." });

            try
            {
                var request = new GetClinicalOrdersRequestModel { HospitalId = hospitalId, AdmissionId = admissionId, OrderType = orderType };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrders for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching orders." });
            }
        }

        // Places an order (one or more lines); chargeable lines are billed immediately.
        [HttpPost]
        public async Task<ActionResult<PlaceClinicalOrderResponseModel>> PlaceOrder([FromBody] PlaceClinicalOrderRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.OrderType))
                return BadRequest(new { Message = "hospitalId, admissionId and orderType are required." });

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
                _logger.LogError(ex, "Error in PlaceOrder for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while placing the order." });
            }
        }

        // Discontinues one line of an order; voids its charge event too, if any.
        [HttpPost("discontinue-line")]
        public async Task<ActionResult<DiscontinueClinicalOrderLineResponseModel>> DiscontinueLine([FromBody] DiscontinueClinicalOrderLineRequestModel request)
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
