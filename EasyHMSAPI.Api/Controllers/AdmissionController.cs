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
    [Route("admission")]
    [Authorize]
    public class AdmissionController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AdmissionController> _logger;

        public AdmissionController(IMediator mediator, ILogger<AdmissionController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Find the admission linked to a billing encounter (null when not admitted).
        [HttpGet]
        public async Task<ActionResult<GetAdmissionByEncounterResponseModel>> GetByEncounter(
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid encounterId)
        {
            if (hospitalId == Guid.Empty || encounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                var request = new GetAdmissionByEncounterRequestModel { HospitalId = hospitalId, EncounterId = encounterId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetByEncounter for encounterId: {EncounterId}", encounterId);
                return StatusCode(500, new { Message = "An error occurred while fetching the admission." });
            }
        }

        // Every currently-open admission for the hospital, with patient name + current bed (if any).
        [HttpGet("active")]
        public async Task<ActionResult<GetActiveAdmissionsResponseModel>> GetActiveAdmissions([FromQuery] Guid hospitalId, [FromQuery] string? statusFilter = null)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetActiveAdmissionsRequestModel { HospitalId = hospitalId, StatusFilter = statusFilter };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetActiveAdmissions for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching active admissions." });
            }
        }

        // Returning-patient detail: full demographics (for re-admit pre-fill) + admission history.
        [HttpGet("patient")]
        public async Task<ActionResult<GetPatientAdmissionsResponseModel>> GetPatientAdmissions(
            [FromQuery] Guid hospitalId,
            [FromQuery] string patientId)
        {
            if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId))
                return BadRequest(new { Message = "hospitalId and patientId are required." });

            try
            {
                var request = new GetPatientAdmissionsRequestModel { HospitalId = hospitalId, PatientId = patientId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientAdmissions for patientId: {PatientId}", patientId);
                return StatusCode(500, new { Message = "An error occurred while fetching patient admissions." });
            }
        }

        // Admit a patient (new IPD admission). Registers a new patient (auto UHID) or reuses an
        // existing one by UHID, then opens an admission with its own IPD number.
        [HttpPost]
        public async Task<ActionResult<AdmitPatientResponseModel>> Admit([FromBody] AdmitPatientRequestModel request)
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
                _logger.LogError(ex, "Error in Admit for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while admitting the patient." });
            }
        }

        // Basic discharge: closes the admission to DISCHARGED and releases its bed, if any.
        [HttpPost("discharge")]
        public async Task<ActionResult<DischargeAdmissionResponseModel>> Discharge([FromBody] DischargeAdmissionRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Discharge for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while discharging the patient." });
            }
        }

        // Any other status transition (DISCHARGE_INITIATED/DISCHARGE_BILLED, LAMA, DAMA,
        // TRANSFERRED_OUT, EXPIRED, CANCELLED). Terminal transitions auto-release the bed.
        [HttpPost("status")]
        public async Task<ActionResult<UpdateAdmissionStatusResponseModel>> UpdateStatus([FromBody] UpdateAdmissionStatusRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ToStatus))
                return BadRequest(new { Message = "hospitalId, admissionId and toStatus are required." });

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
                _logger.LogError(ex, "Error in UpdateStatus for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while updating the admission status." });
            }
        }

        // Confirms a PRE_ADMIT (elective pre-registration) admission has physically arrived:
        // flips it to ADMITTED and optionally assigns a bed in the same transaction.
        [HttpPost("confirm-arrival")]
        public async Task<ActionResult<ConfirmPatientArrivalResponseModel>> ConfirmArrival([FromBody] ConfirmPatientArrivalRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in ConfirmArrival for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while confirming arrival." });
            }
        }

        // Edits fields captured at admission time (consultant, diagnosis, referral, payer, deposit,
        // expected discharge) — only while the admission is still active.
        [HttpPut("details")]
        public async Task<ActionResult<UpdateAdmissionDetailsResponseModel>> UpdateDetails([FromBody] UpdateAdmissionDetailsRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in UpdateDetails for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while updating admission details." });
            }
        }

        // Upserts the admission's coverage row (payer name, policy/pre-auth/package, sanctioned
        // amount, entitled room category) — only while the admission is still active.
        [HttpPut("coverage")]
        public async Task<ActionResult<UpsertAdmissionCoverageResponseModel>> UpsertCoverage([FromBody] UpsertAdmissionCoverageRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in UpsertCoverage for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while updating coverage details." });
            }
        }
    }
}
