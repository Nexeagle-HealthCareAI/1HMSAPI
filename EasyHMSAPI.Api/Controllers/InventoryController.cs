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
    [RequiresPermission("inventory")]
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

        // Board-level "use stock, bill the patient" quick action (ICU board today) — one call that
        // deducts stock and posts the matching charge event together.
        [HttpPost("use-and-bill")]
        public async Task<ActionResult<RecordAndBillStockUsageResponseModel>> UseAndBillStock([FromBody] RecordAndBillStockUsageRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.StoreId == Guid.Empty || request.InventoryItemId == Guid.Empty
                || request.EncounterId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
                return BadRequest(new { Message = "hospitalId, storeId, inventoryItemId, encounterId, and patientId are required." });

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
                _logger.LogError(ex, "Error in UseAndBillStock for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording and billing stock usage." });
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

        [HttpGet("batches/by-barcode")]
        public async Task<ActionResult<GetBatchByBarcodeResponseModel>> GetBatchByBarcode(
            [FromQuery] Guid hospitalId, [FromQuery] string barcodeValue, [FromQuery] Guid? storeId)
        {
            if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(barcodeValue))
                return BadRequest(new { Message = "hospitalId and barcodeValue are required." });

            try
            {
                var response = await _mediator.Send(new GetBatchByBarcodeRequestModel
                {
                    HospitalId = hospitalId,
                    StoreId = storeId,
                    BarcodeValue = barcodeValue,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBatchByBarcode for barcode: {BarcodeValue}", barcodeValue);
                return StatusCode(500, new { Message = "An error occurred while looking up the barcode." });
            }
        }

        // Pharmacy Stock/Batches tab — flat, hospital-wide "everything currently in stock" view for
        // browsing/verifying what's already there, unlike GetBatches (one item) or the near-expiry
        // report (90-day expiry window only).
        [HttpGet("batches")]
        public async Task<ActionResult<GetAllBatchesResponseModel>> GetAllBatches(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? storeId, [FromQuery] string? search, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetAllBatchesRequestModel
                {
                    HospitalId = hospitalId,
                    StoreId = storeId,
                    Search = search,
                    ActiveOnly = activeOnly,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllBatches for hospitalId: {HospitalId}", hospitalId);
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

        // Pharmacy Phase 3c — parses+validates a distributor .csv/.xlsx without writing anything;
        // frontend shows the grid for correction, then posts the fixed rows to batches/bulk below.
        [HttpPost("batches/bulk-import/preview")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<PreviewBulkImportResponseModel>> PreviewBulkImport([FromForm] PreviewBulkImportRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PreviewBulkImport for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while previewing the import file." });
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

        // Pharmacy Phase 3b — store/supplier-filterable near-expiry report (Green >180d / Yellow
        // 90-180d / Orange 30-90d / Red <30d, matching FEFO's own lockout cutoff).
        [HttpGet("expiry/near-expiry-report")]
        public async Task<ActionResult<GetNearExpiryReportResponseModel>> GetNearExpiryReport(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? storeId, [FromQuery] Guid? vendorId, [FromQuery] string? bucket)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetNearExpiryReportRequestModel
                {
                    HospitalId = hospitalId,
                    StoreId = storeId,
                    VendorId = vendorId,
                    Bucket = bucket,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNearExpiryReport for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the near-expiry report." });
            }
        }

        // Pharmacy Phase 3b — Schedule H1 statutory register (Drugs & Cosmetics Rules).
        [HttpGet("schedule-register")]
        public async Task<ActionResult<GetDrugScheduleRegisterResponseModel>> GetDrugScheduleRegister(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? inventoryItemId, [FromQuery] string? scheduleClass)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetDrugScheduleRegisterRequestModel
                {
                    HospitalId = hospitalId,
                    InventoryItemId = inventoryItemId,
                    ScheduleClass = scheduleClass,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDrugScheduleRegister for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the drug schedule register." });
            }
        }

        // Pharmacy Phase 3c — weekly/monthly auto-threshold suggestions from trailing consumption.
        [HttpGet("reorder-threshold-suggestions")]
        public async Task<ActionResult<GetReorderThresholdSuggestionsResponseModel>> GetReorderThresholdSuggestions(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? storeId, [FromQuery] decimal bufferMultiplier = 1.5m)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetReorderThresholdSuggestionsRequestModel
                {
                    HospitalId = hospitalId,
                    StoreId = storeId,
                    BufferMultiplier = bufferMultiplier,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReorderThresholdSuggestions for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while computing reorder threshold suggestions." });
            }
        }

        [HttpPost("reorder-threshold-suggestions/accept")]
        public async Task<ActionResult<AcceptThresholdSuggestionResponseModel>> AcceptThresholdSuggestion([FromBody] AcceptThresholdSuggestionRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and inventoryItemId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AcceptThresholdSuggestion for inventoryItemId: {InventoryItemId}", request.InventoryItemId);
                return StatusCode(500, new { Message = "An error occurred while accepting the threshold suggestion." });
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
