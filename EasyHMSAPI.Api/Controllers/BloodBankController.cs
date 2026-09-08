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
    // Hospital-wide blood bank — pool search, cross-match/reserve, transfuse. Any admission, not
    // gated behind OT/surgery.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("blood-bank")]
    [Authorize]
    public class BloodBankController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BloodBankController> _logger;

        public BloodBankController(IMediator mediator, ILogger<BloodBankController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("pool")]
        public async Task<ActionResult<GetBloodBagPoolResponseModel>> GetPool(
            [FromQuery] Guid hospitalId, [FromQuery] string? component, [FromQuery] string? bloodGroup)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetBloodBagPoolRequestModel { HospitalId = hospitalId, Component = component, BloodGroup = bloodGroup });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPool for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the blood bag pool." });
            }
        }

        [HttpGet("inventory")]
        public async Task<ActionResult<GetBloodBankInventoryResponseModel>> GetInventory([FromQuery] Guid hospitalId, [FromQuery] string? status)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetBloodBankInventoryRequestModel { HospitalId = hospitalId, Status = status });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInventory for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching blood bank inventory." });
            }
        }

        [HttpGet("ledger")]
        public async Task<ActionResult<GetBloodBankLedgerResponseModel>> GetLedger([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetBloodBankLedgerRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLedger for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the transfusion ledger." });
            }
        }

        [HttpGet("admission/{admissionId:guid}/history")]
        public async Task<ActionResult<GetAdmissionTransfusionHistoryResponseModel>> GetHistory([FromQuery] Guid hospitalId, Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetAdmissionTransfusionHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching transfusion history." });
            }
        }

        [HttpPost("bag")]
        public async Task<ActionResult<ReceiveBloodBagResponseModel>> ReceiveBag([FromBody] ReceiveBloodBagRequestModel request)
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
                _logger.LogError(ex, "Error in ReceiveBag for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while receiving the blood bag." });
            }
        }

        [HttpPost("bag/reserve")]
        public async Task<ActionResult<ReserveBloodBagResponseModel>> ReserveBag([FromBody] ReserveBloodBagRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and bloodBagId are required." });

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
                _logger.LogError(ex, "Error in ReserveBag for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while reserving the blood bag." });
            }
        }

        [HttpPost("bag/discard")]
        public async Task<ActionResult<DiscardBloodBagResponseModel>> DiscardBag([FromBody] DiscardBloodBagRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and bloodBagId are required." });

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
                _logger.LogError(ex, "Error in DiscardBag for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while discarding the blood bag." });
            }
        }

        [HttpPost("transfuse")]
        public async Task<ActionResult<RecordTransfusionResponseModel>> Transfuse([FromBody] RecordTransfusionRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, bloodBagId, and admissionId are required." });

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
                _logger.LogError(ex, "Error in Transfuse for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while recording the transfusion." });
            }
        }
    }
}
