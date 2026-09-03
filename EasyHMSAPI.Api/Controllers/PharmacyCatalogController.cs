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
    // Pharmacy Phase 3c — Molecule/SaltComposition catalog (global, not per-hospital) driving
    // 1-click generic substitution in the POS.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("pharmacy-catalog")]
    [Authorize]
    [RequiresPermission("pharmacy")]
    public class PharmacyCatalogController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacyCatalogController> _logger;

        public PharmacyCatalogController(IMediator mediator, ILogger<PharmacyCatalogController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("molecules")]
        public async Task<ActionResult<GetMoleculesResponseModel>> GetMolecules([FromQuery] string? search)
        {
            try
            {
                return Ok(await _mediator.Send(new GetMoleculesRequestModel { Search = search }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMolecules");
                return StatusCode(500, new { Message = "An error occurred while fetching molecules." });
            }
        }

        [HttpPost("molecules")]
        public async Task<ActionResult<CreateMoleculeResponseModel>> CreateMolecule([FromBody] CreateMoleculeRequestModel request)
        {
            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateMolecule");
                return StatusCode(500, new { Message = "An error occurred while creating the molecule." });
            }
        }

        [HttpGet("salt-compositions")]
        public async Task<ActionResult<GetSaltCompositionsResponseModel>> GetSaltCompositions([FromQuery] string? search)
        {
            try
            {
                return Ok(await _mediator.Send(new GetSaltCompositionsRequestModel { Search = search }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSaltCompositions");
                return StatusCode(500, new { Message = "An error occurred while fetching salt compositions." });
            }
        }

        [HttpPost("salt-compositions")]
        public async Task<ActionResult<CreateSaltCompositionResponseModel>> CreateSaltComposition([FromBody] CreateSaltCompositionRequestModel request)
        {
            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateSaltComposition");
                return StatusCode(500, new { Message = "An error occurred while creating the salt composition." });
            }
        }

        // 1-click generic switcher — alternates in stock at the given store, sharing the item's
        // SaltCompositionId, cheapest first.
        [HttpGet("substitutes")]
        public async Task<ActionResult<GetSubstituteItemsResponseModel>> GetSubstitutes(
            [FromQuery] Guid hospitalId, [FromQuery] Guid inventoryItemId, [FromQuery] Guid? storeId)
        {
            if (hospitalId == Guid.Empty || inventoryItemId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and inventoryItemId are required." });

            try
            {
                return Ok(await _mediator.Send(new GetSubstituteItemsRequestModel
                {
                    HospitalId = hospitalId,
                    InventoryItemId = inventoryItemId,
                    StoreId = storeId,
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSubstitutes for inventoryItemId: {InventoryItemId}", inventoryItemId);
                return StatusCode(500, new { Message = "An error occurred while fetching substitutes." });
            }
        }
    }
}
