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
    // Inventory store hierarchy (central/ward/OT/pharmacy/CSSD/blood-bank/etc.) — the location
    // dimension every stock movement, batch, and unified stock-visibility view keys off.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory/stores")]
    [Authorize]
    public class StoreController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<StoreController> _logger;

        public StoreController(IMediator mediator, ILogger<StoreController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<GetStoresResponseModel>> GetStores([FromQuery] Guid hospitalId, [FromQuery] bool includeInactive = false)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetStoresRequestModel { HospitalId = hospitalId, IncludeInactive = includeInactive });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStores for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching stores." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<UpsertStoreResponseModel>> UpsertStore([FromBody] UpsertStoreRequestModel request)
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
                _logger.LogError(ex, "Error in UpsertStore for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the store." });
            }
        }
    }
}
