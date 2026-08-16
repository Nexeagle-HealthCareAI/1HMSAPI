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
    // Purchase orders — created directly or from an approved Indent; received against via GRN.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory/purchase-orders")]
    [Authorize]
    [RequiresPermission("inventory")]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PurchaseOrderController> _logger;

        public PurchaseOrderController(IMediator mediator, ILogger<PurchaseOrderController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetPurchaseOrdersResponseModel>> GetPurchaseOrders([FromQuery] Guid hospitalId, [FromQuery] string? status)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetPurchaseOrdersRequestModel { HospitalId = hospitalId, Status = status });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPurchaseOrders for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching purchase orders." });
            }
        }

        [HttpGet("{purchaseOrderId:guid}")]
        public async Task<ActionResult<GetPurchaseOrderDetailResponseModel>> GetPurchaseOrderDetail(Guid purchaseOrderId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || purchaseOrderId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and purchaseOrderId are required." });

            try
            {
                var response = await _mediator.Send(new GetPurchaseOrderDetailRequestModel { HospitalId = hospitalId, PurchaseOrderId = purchaseOrderId });
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPurchaseOrderDetail for purchaseOrderId: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, new { Message = "An error occurred while fetching the purchase order." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<CreatePurchaseOrderResponseModel>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreatePurchaseOrder for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the purchase order." });
            }
        }

        [HttpPost("{purchaseOrderId:guid}/approve")]
        public async Task<ActionResult<PurchaseOrderActionResponseModel>> ApprovePurchaseOrder(Guid purchaseOrderId, [FromBody] ApprovePurchaseOrderRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.PurchaseOrderId = purchaseOrderId;

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApprovePurchaseOrder for purchaseOrderId: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, new { Message = "An error occurred while approving the purchase order." });
            }
        }

        [HttpPost("{purchaseOrderId:guid}/mark-sent")]
        public async Task<ActionResult<PurchaseOrderActionResponseModel>> MarkSent(Guid purchaseOrderId, [FromBody] MarkPurchaseOrderSentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.PurchaseOrderId = purchaseOrderId;

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
                _logger.LogError(ex, "Error in MarkSent for purchaseOrderId: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, new { Message = "An error occurred while marking the purchase order as sent." });
            }
        }

        [HttpPost("{purchaseOrderId:guid}/cancel")]
        public async Task<ActionResult<PurchaseOrderActionResponseModel>> CancelPurchaseOrder(Guid purchaseOrderId, [FromBody] CancelPurchaseOrderRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            request.PurchaseOrderId = purchaseOrderId;

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
                _logger.LogError(ex, "Error in CancelPurchaseOrder for purchaseOrderId: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, new { Message = "An error occurred while cancelling the purchase order." });
            }
        }
    }
}
