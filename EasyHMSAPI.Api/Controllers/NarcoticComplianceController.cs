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
    // India regulatory compliance — the only path a NARCOTIC-scheduled item can be dispensed
    // through, the resulting register, and cold-chain temperature logging.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("inventory")]
    [Authorize]
    public class NarcoticComplianceController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NarcoticComplianceController> _logger;

        public NarcoticComplianceController(IMediator mediator, ILogger<NarcoticComplianceController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("narcotics/dispense")]
        public async Task<ActionResult<DispenseNarcoticResponseModel>> DispenseNarcotic([FromBody] DispenseNarcoticRequestModel request)
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
                _logger.LogError(ex, "Error in DispenseNarcotic for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while dispensing the narcotic." });
            }
        }

        [HttpGet("narcotics/register")]
        public async Task<ActionResult<GetNarcoticRegisterResponseModel>> GetNarcoticRegister(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? inventoryItemId, [FromQuery] string? formType)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetNarcoticRegisterRequestModel { HospitalId = hospitalId, InventoryItemId = inventoryItemId, FormType = formType });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNarcoticRegister for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the narcotics register." });
            }
        }

        [HttpPost("cold-chain/readings")]
        public async Task<ActionResult<RecordColdChainReadingResponseModel>> RecordColdChainReading([FromBody] RecordColdChainReadingRequestModel request)
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
                _logger.LogError(ex, "Error in RecordColdChainReading for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the reading." });
            }
        }

        [HttpGet("cold-chain/readings")]
        public async Task<ActionResult<GetColdChainReadingsResponseModel>> GetColdChainReadings([FromQuery] Guid hospitalId, [FromQuery] Guid? storeId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetColdChainReadingsRequestModel { HospitalId = hospitalId, StoreId = storeId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetColdChainReadings for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching cold-chain readings." });
            }
        }
    }
}
