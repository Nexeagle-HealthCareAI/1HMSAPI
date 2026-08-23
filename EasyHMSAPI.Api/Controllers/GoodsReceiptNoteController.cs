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
    // Goods receipt — the point where Batch rows are actually created (batch/expiry captured at
    // receipt) and stock lands, via the shared RecordInventoryMovement handler.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory/grn")]
    [Authorize]
    [RequiresPermission("inventory")]
    public class GoodsReceiptNoteController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<GoodsReceiptNoteController> _logger;

        public GoodsReceiptNoteController(IMediator mediator, ILogger<GoodsReceiptNoteController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetGoodsReceiptNotesResponseModel>> GetGoodsReceiptNotes([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetGoodsReceiptNotesRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetGoodsReceiptNotes for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching goods receipt notes." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<CreateGoodsReceiptNoteResponseModel>> CreateGoodsReceiptNote([FromBody] CreateGoodsReceiptNoteRequestModel request)
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
                _logger.LogError(ex, "Error in CreateGoodsReceiptNote for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the goods receipt." });
            }
        }
    }
}
