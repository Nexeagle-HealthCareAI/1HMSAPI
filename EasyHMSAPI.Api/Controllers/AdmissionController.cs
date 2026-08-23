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
    [RequiresPermission("ipd")]
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

        // Reassigns the admission's admitting doctor: releases the current ACTIVE
        // AdmissionDoctorAssignment row and creates a new one atomically, and updates
        // Admission.PrimaryDoctorId. Only while the admission is still active.
        [HttpPost("doctor")]
        public async Task<ActionResult<ChangeAdmittingDoctorResponseModel>> ChangeDoctor([FromBody] ChangeAdmittingDoctorRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.DoctorId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, admissionId and doctorId are required." });

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
                _logger.LogError(ex, "Error in ChangeDoctor for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while changing the admitting doctor." });
            }
        }

        // Full assignment history for the admission's admitting doctor -- each row is one doctor's
        // tenure span (AssignedAt -> UnassignedAt, or "current" while ACTIVE).
        [HttpGet("doctor/history")]
        public async Task<ActionResult<GetAdmissionDoctorHistoryResponseModel>> GetDoctorHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var request = new GetAdmissionDoctorHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDoctorHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching doctor history." });
            }
        }

        // Reassigns the admission's "Referred by": releases the current ACTIVE
        // AdmissionReferrerAssignment row and creates a new one atomically, and updates
        // Admission.ReferralSource/ReferralName/ReferredByReferrerId. Only while the admission is
        // still active.
        [HttpPost("referrer")]
        public async Task<ActionResult<ChangeAdmissionReferrerResponseModel>> ChangeReferrer([FromBody] ChangeAdmissionReferrerRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReferralSource))
                return BadRequest(new { Message = "hospitalId, admissionId and referralSource are required." });

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
                _logger.LogError(ex, "Error in ChangeReferrer for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while changing the referrer." });
            }
        }

        // Full assignment history for the admission's "Referred by" -- each row is one referrer's
        // tenure span (AssignedAt -> UnassignedAt, or "current" while ACTIVE).
        [HttpGet("referrer/history")]
        public async Task<ActionResult<GetAdmissionReferrerHistoryResponseModel>> GetReferrerHistory([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var request = new GetAdmissionReferrerHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReferrerHistory for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching referrer history." });
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

        // Uploads a general-purpose document (insurance card, ID proof, referral letter, scanned
        // report, etc.) against the admission -- listed on the Patient Workspace's Documents tab.
        // Not gated on admission-active status: paperwork routinely arrives after discharge.
        [HttpPost("document/upload")]
        public async Task<ActionResult<UploadAdmissionDocumentResponseModel>> UploadDocument(UploadAdmissionDocumentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.File == null)
                return BadRequest(new { Message = "hospitalId, admissionId and a file are required." });

            try
            {
                request.UploadedByUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadDocument for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while uploading the document." });
            }
        }

        [HttpGet("document/list")]
        public async Task<ActionResult<GetAdmissionDocumentsResponseModel>> GetDocuments([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var request = new GetAdmissionDocumentsRequestModel { HospitalId = hospitalId, AdmissionId = admissionId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDocuments for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while fetching documents." });
            }
        }

        [HttpDelete("document/delete")]
        public async Task<ActionResult<DeleteAdmissionDocumentResponseModel>> DeleteDocument([FromQuery] DeleteAdmissionDocumentRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.DocumentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, admissionId and documentId are required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteDocument for documentId: {DocumentId}", request.DocumentId);
                return StatusCode(500, new { Message = "An error occurred while deleting the document." });
            }
        }
    }
}
