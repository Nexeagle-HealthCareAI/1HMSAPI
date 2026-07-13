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
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("charge")]
    [Authorize]
    public class ChargeController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ChargeController> _logger;

        public ChargeController(IMediator mediator, ILogger<ChargeController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("master")]
        public async Task<ActionResult<GetChargeMastersResponseModel>> GetChargeMasters([FromQuery] Guid hospitalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetChargeMastersRequestModel { HospitalId = hospitalId, Page = page, PageSize = pageSize };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetChargeMasters for hospitalId: {HospitalId}", hospitalId);
                // TEMP diagnostic: surface the real error so we can see the root cause in the response.
                return StatusCode(500, new
                {
                    Message = "An error occurred.",
                    Detail = ex.Message,
                    Inner = ex.InnerException?.Message,
                    Type = ex.GetType().Name
                });
            }
        }

        [HttpGet("master/{chargeId}")]
        public async Task<ActionResult<GetChargeMasterByIdResponseModel>> GetChargeMasterById(Guid chargeId, [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || chargeId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and ChargeId are required." });

            try
            {
                var request = new GetChargeMasterByIdRequestModel { HospitalId = hospitalId, ChargeId = chargeId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Charge not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetChargeMasterById for chargeId: {ChargeId}", chargeId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("master")]
        public async Task<ActionResult<UpsertChargeMasterResponseModel>> UpsertChargeMaster([FromBody] UpsertChargeMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertChargeMaster for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPatch("master/status")]
        public async Task<ActionResult<UpdateChargeMasterStatusResponseModel>> UpdateChargeMasterStatus([FromQuery] Guid chargeId, [FromQuery] Guid hospitalId, [FromBody] UpdateChargeMasterStatusRequestModel request)
        {
            if (hospitalId == Guid.Empty || chargeId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and ChargeId are required." });

            try
            {
                request.HospitalId = hospitalId;
                request.ChargeId = chargeId;
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateChargeMasterStatus for chargeId: {ChargeId}", chargeId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpDelete("master")]
        public async Task<ActionResult<DeleteChargeMasterResponseModel>> DeleteChargeMaster([FromBody] DeleteChargeMasterRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ChargeId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and ChargeId are required." });

            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteChargeMaster for chargeId: {ChargeId}", request.ChargeId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // ── Charge events (bill lines) ────────────────────────────────────────

        [HttpPost("create-event")]
        public async Task<ActionResult<CreateChargeEventResponseModel>> CreateChargeEvent([FromBody] CreateChargeEventRequestModel request)
        {
            if (string.IsNullOrEmpty(request.PatientId) || request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "PatientId and HospitalId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateChargeEvent for patientId: {PatientId}", request.PatientId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("encounter")]
        public async Task<ActionResult<CreateManualEncounterResponseModel>> CreateManualEncounter([FromBody] CreateManualEncounterRequestModel request)
        {
            if (string.IsNullOrEmpty(request.PatientId) || request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "PatientId and HospitalId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateManualEncounter for patientId: {PatientId}", request.PatientId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("add-event")]
        public async Task<ActionResult<AddChargeEventResponseModel>> AddChargeEvents([FromBody] AddChargeEventRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrEmpty(request.PatientId) || request.EncounterId == Guid.Empty || request.Charges == null || request.Charges.Count == 0)
                return BadRequest(new { Message = "HospitalId, PatientId, EncounterId and Charges are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                request.IdempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddChargeEvents for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("update-event")]
        public async Task<ActionResult<UpdateChargeEventResponseModel>> UpdateChargeEvent([FromBody] UpdateChargeEventRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ChargeEventId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and ChargeEventId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateChargeEvent for chargeEventId: {ChargeEventId}", request.ChargeEventId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPatch("cancel-event")]
        public async Task<ActionResult<CancelChargeEventResponseModel>> CancelChargeEvent([FromBody] CancelChargeEventRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrEmpty(request.PatientId))
                return BadRequest(new { Message = "HospitalId and PatientId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CancelChargeEvent for patientId: {PatientId}", request.PatientId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // ── Rate cards (payer-type override + room-class multiplier) ──────────

        [HttpGet("rate-card")]
        public async Task<ActionResult<GetRateCardConfigResponseModel>> GetRateCardConfig([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetRateCardConfigRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRateCardConfig for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("rate-card/payer-rate")]
        public async Task<ActionResult<UpsertChargeMasterPayerRateResponseModel>> UpsertChargeMasterPayerRate([FromBody] UpsertChargeMasterPayerRateRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.ChargeId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId and ChargeId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertChargeMasterPayerRate for chargeId: {ChargeId}", request.ChargeId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPut("rate-card/room-multiplier")]
        public async Task<ActionResult<UpsertRoomClassRateMultiplierResponseModel>> UpsertRoomClassRateMultiplier([FromBody] UpsertRoomClassRateMultiplierRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrEmpty(request.RoomType))
                return BadRequest(new { Message = "HospitalId and RoomType are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertRoomClassRateMultiplier for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
