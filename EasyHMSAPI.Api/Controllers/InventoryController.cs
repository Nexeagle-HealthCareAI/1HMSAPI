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
    // Pharmacy/consumable/implant inventory — item master + batch/store-aware stock movements.
    // RecordMovement is also called directly via nested mediator send from OT (IntraOpItemUsage)
    // and CSSD handlers, passing neither BatchId nor StoreId — the legacy CurrentStock-only path.
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

        [HttpPost("transfer")]
        public async Task<ActionResult<TransferStockResponseModel>> TransferStock([FromBody] TransferStockRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

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
                _logger.LogError(ex, "Error in TransferStock for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while transferring stock." });
            }
        }

        // Board-level "receive stock" quick action (OT/ICU boards) — one call that creates a batch
        // and records the inbound movement together, so clinical staff don't have to make two
        // separate requests to safely stock a new consignment.
        [HttpPost("receive")]
        public async Task<ActionResult<QuickReceiveStockResponseModel>> QuickReceive([FromBody] QuickReceiveStockRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.StoreId == Guid.Empty || request.InventoryItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, storeId, and inventoryItemId are required." });

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
                _logger.LogError(ex, "Error in QuickReceive for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while receiving stock." });
            }
        }

        [HttpGet("items/{inventoryItemId:guid}/batches")]
        public async Task<ActionResult<GetBatchesForItemResponseModel>> GetBatches(
            Guid inventoryItemId, [FromQuery] Guid hospitalId, [FromQuery] Guid? storeId, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty || inventoryItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and inventoryItemId are required." });

            try
            {
                var response = await _mediator.Send(new GetBatchesForItemRequestModel
                {
                    HospitalId = hospitalId,
                    InventoryItemId = inventoryItemId,
                    StoreId = storeId,
                    ActiveOnly = activeOnly,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBatches for inventoryItemId: {InventoryItemId}", inventoryItemId);
                return StatusCode(500, new { Message = "An error occurred while fetching batches." });
            }
        }

        [HttpPost("batches")]
        public async Task<ActionResult<CreateBatchResponseModel>> CreateBatch([FromBody] CreateBatchRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty || request.StoreId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, inventoryItemId, and storeId are required." });

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
                _logger.LogError(ex, "Error in CreateBatch for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating the batch." });
            }
        }

        [HttpPost("batches/bulk")]
        public async Task<ActionResult<CreateBulkBatchResponseModel>> CreateBulkBatch([FromBody] CreateBulkBatchRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return response.Success ? Ok(response) : BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateBulkBatch for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while creating bulk batches." });
            }
        }

        // Hospital-wide board: stock-by-store, expiry alerts (90/60/30-day tiers), reorder alerts.
        [HttpGet("board")]
        public async Task<ActionResult<GetInventoryBoardResponseModel>> GetBoard([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetInventoryBoardRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBoard for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the inventory board." });
            }
        }

        // Unified "everything, every store" view — InventoryItem/StockLevel, BloodBag, and
        // InstrumentSet combined (INV-10). Read-only; each module's own screen/API is unchanged.
        [HttpGet("unified-stock")]
        public async Task<ActionResult<GetUnifiedStockVisibilityResponseModel>> GetUnifiedStock([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetUnifiedStockVisibilityRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUnifiedStock for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching unified stock visibility." });
            }
        }
    }
}
