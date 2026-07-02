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
    // Pharmacy/consumable/implant inventory — item master + stock movements. Consumed directly by
    // OT (IntraOpItemUsage) and CSSD handlers via nested mediator send; this controller exists for
    // any direct item-master/receive/adjust needs, not a full stock-management screen this phase.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IMediator mediator, ILogger<InventoryController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("items")]
        public async Task<ActionResult<GetInventoryItemsResponseModel>> GetItems(
            [FromQuery] Guid hospitalId, [FromQuery] string? category, [FromQuery] string? search, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetInventoryItemsRequestModel
                {
                    HospitalId = hospitalId,
                    Category = category,
                    Search = search,
                    ActiveOnly = activeOnly,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetItems for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching inventory items." });
            }
        }

        [HttpPost("items")]
        public async Task<ActionResult<CreateInventoryItemResponseModel>> CreateItem([FromBody] CreateInventoryItemRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

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
                _logger.LogError(ex, "Error in CreateItem for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the inventory item." });
            }
        }

        [HttpPost("items/movement")]
        public async Task<ActionResult<RecordInventoryMovementResponseModel>> RecordMovement([FromBody] RecordInventoryMovementRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and inventoryItemId are required." });

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
                _logger.LogError(ex, "Error in RecordMovement for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the inventory movement." });
            }
        }
    }
}
