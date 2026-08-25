using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
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
    [Route("hospitals")]
    [ApiController]
    public class HospitalsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HospitalsController> _logger;
        public HospitalsController(IMediator mediator, ILogger<HospitalsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("register")]
        [Authorize]
        public async Task<ActionResult<HospitalRegisterResponseModel>> RegisterHospital([FromBody] HospitalRegisterRequestModel request)
        {
            _logger.LogInformation("RegisterHospital started at {Time}", DateTime.UtcNow);
            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                _logger.LogInformation("RegisterHospital ended");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterHospital");
                var errorMsg = ex.InnerException != null ? $"{ex.Message} Inner: {ex.InnerException.Message}" : ex.Message;
                return StatusCode(500, new { Message = "An error occurred while registering hospital", Error = errorMsg });
            }
        }

        [HttpPut("{hospitalId}")]
        [Authorize]
        public async Task<ActionResult<HospitalUpdateResponseModel>> UpdateHospital(Guid hospitalId, [FromBody] HospitalUpdateRequestModel request)
        {
            _logger.LogInformation("UpdateHospital started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    return BadRequest("Hospital ID is required and cannot be empty.");
                }

                request.HospitalId = hospitalId;

                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdateHospital ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateHospital for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating hospital", Error = ex.Message });
            }
        }

        [HttpPatch("{hospitalId}/deactivate")]
        [Authorize]
        public async Task<ActionResult<DeactivateHospitalResponseModel>> DeactivateHospital(Guid hospitalId)
        {
            _logger.LogInformation("DeactivateHospital started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty)
                    return BadRequest(new { Message = "Hospital ID is required and cannot be empty." });

                var userId = UserContextHelper.GetUserId(HttpContext.User);
                if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });

                var request = new DeactivateHospitalRequestModel { HospitalId = hospitalId, CallerUserId = userId.Value };
                var response = await _mediator.Send(request);
                _logger.LogInformation("DeactivateHospital ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeactivateHospital for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while deactivating the hospital." });
            }
        }

        // Idempotent -- issues a HospitalCode if this hospital doesn't have one yet (returns the
        // existing one otherwise), for staff to print onto an OPD QR code.
        [HttpPost("{hospitalId}/generate-code")]
        [Authorize]
        public async Task<ActionResult<GenerateHospitalCodeResponseModel>> GenerateHospitalCode(Guid hospitalId)
        {
            try
            {
                if (hospitalId == Guid.Empty)
                    return BadRequest(new { Message = "Hospital ID is required and cannot be empty." });

                var response = await _mediator.Send(new GenerateHospitalCodeRequestModel { HospitalId = hospitalId });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateHospitalCode for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while generating the hospital code." });
            }
        }

        // Ready-to-print PNG (NexEagle logo centered) encoding this hospital's check-in URL --
        // requires a HospitalCode to already exist (see GenerateHospitalCode above).
        [HttpGet("{hospitalId}/qr-code")]
        [Authorize]
        public async Task<IActionResult> GetHospitalQrCode(Guid hospitalId)
        {
            try
            {
                if (hospitalId == Guid.Empty)
                    return BadRequest(new { Message = "Hospital ID is required and cannot be empty." });

                var response = await _mediator.Send(new GetHospitalQrCodeRequestModel { HospitalId = hospitalId });
                if (!response.Success || response.Content == null) return BadRequest(new { response.Message });
                return File(response.Content, response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalQrCode for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while generating the QR code." });
            }
        }

        [HttpGet("{hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalDetailsResponseModel>> GetHospitalById(Guid hospitalId)
        {
            _logger.LogInformation("GetHospitalById started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    return BadRequest("Hospital ID is required and cannot be empty.");
                }

                var request = new GetHospitalDetailsRequestModel(hospitalId);
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetHospitalById ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalById for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital details", Error = ex.Message });
            }
        }

        // All hospitals the signed-in user belongs to (across any chain) — powers the switcher.
        [HttpGet("mine")]
        [Authorize]
        public async Task<ActionResult<GetMyHospitalsResponseModel>> GetMyHospitals()
        {
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null)
                return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                var response = await _mediator.Send(new GetMyHospitalsRequestModel { UserId = userId.Value });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMyHospitals for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving your hospitals." });
            }
        }

        [HttpGet("users/{userId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalUsersResponseModel>> GetHospitalUserById(Guid userId)
        {
            _logger.LogInformation("GetHospitalUserById started at {Time} for userId: {UserId}", DateTime.UtcNow, userId);
            try
            {
                if(userId == Guid.Empty )
                {
                    return BadRequest("User ID is required and cannot be empty.");
                }
                var request = new GetHospitalUsersRequestModel(userId);
                var response = await _mediator.Send(request);
                _logger.LogInformation("GetHospitalUserById ended for userId: {UserId}", userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalUserById for userId: {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while retrieving hospital user", Error = ex.Message });
            }
        }

        [HttpGet("analysis/hospitalId={hospitalId}")]
        [Authorize]
        public async Task<ActionResult<GetHospitalOverallAnalysisResponseModel>> GetHospitalOverallAnalysis(Guid hospitalId)
        {
            _logger.LogInformation("GetAllHospitals started at {Time}", DateTime.UtcNow);
            try
            {
                GetHospitalOverallAnalysisRequestModel requestModel = new() { HospitalId = hospitalId };
                var response = await _mediator.Send(requestModel);
                _logger.LogInformation("GetAllHospitals ended");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllHospitals");
                return StatusCode(500, new { Message = "An error occurred while retrieving all hospitals" + ex.Message + ex.InnerException + ex.StackTrace });
            }
        }

        [HttpGet("analytics/patient-volume-forecast")]
        [Authorize]
        public async Task<ActionResult<GetPatientVolumeForecastResponseModel>> GetPatientVolumeForecast([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetPatientVolumeForecastRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientVolumeForecast for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while generating the patient volume forecast." });
            }
        }

        [HttpGet("analytics/lapsed-patients")]
        [Authorize]
        public async Task<ActionResult<GetLapsedPatientsResponseModel>> GetLapsedPatients([FromQuery] Guid hospitalId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetLapsedPatientsRequestModel { HospitalId = hospitalId, Page = page, Limit = limit };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLapsedPatients for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving lapsed patients." });
            }
        }
    }
}
