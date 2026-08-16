using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("patient")]
    [ApiController]
    [RequiresPermission("patients")]
    public class PatientController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AppointmentsController> _logger;
        public PatientController(IMediator mediator, ILogger<AppointmentsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchPatient([FromQuery] string  searchText, [FromQuery] Guid hospitalId)
        {
            _logger.LogInformation("SearchPatient started a");
            if(string.IsNullOrEmpty(searchText) || hospitalId == Guid.Empty)
            {
                throw new ArgumentException("Search text and HospitalId are required.");
            }

            try
            {
                var request = new SearchPatientRequestModel
                {
                   SearchText = searchText,
                   HospitalId = hospitalId
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("SearchPatient ended successfully for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchPatient, hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { ex.Message });
            }
        }

        // Advisory duplicate detection before a new UHID is created (admission + appointment).
        [HttpPost("check-duplicates")]
        [Authorize]
        public async Task<ActionResult<CheckPatientDuplicatesResponseModel>> CheckDuplicates([FromBody] CheckPatientDuplicatesRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckDuplicates for hospitalId: {HospitalId}", request.HospitalId);
                // Advisory — never surface as an error that blocks registration.
                return Ok(new CheckPatientDuplicatesResponseModel { Success = false, Message = "Error checking duplicates." });
            }
        }

        // Linked-record counts for one UHID — powers the merge preview.
        [HttpGet("record-counts")]
        [Authorize]
        public async Task<ActionResult<GetPatientRecordCountsResponseModel>> GetRecordCounts([FromQuery] Guid hospitalId, [FromQuery] string patientId)
        {
            if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId))
                return BadRequest(new { Message = "hospitalId and patientId are required." });
            try
            {
                var response = await _mediator.Send(new GetPatientRecordCountsRequestModel { HospitalId = hospitalId, PatientId = patientId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRecordCounts for patientId: {PatientId}", patientId);
                return StatusCode(500, new { Message = "An error occurred while fetching record counts." });
            }
        }

        // Admin: merge a duplicate UHID into a canonical one (repoints all linked records).
        [HttpPost("merge")]
        [Authorize]
        public async Task<ActionResult<MergePatientsResponseModel>> Merge([FromBody] MergePatientsRequestModel request)
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
                _logger.LogError(ex, "Error in Merge canonical={Canonical} duplicate={Duplicate}", request.CanonicalPatientId, request.DuplicatePatientId);
                return StatusCode(500, new { Message = "An error occurred while merging patients." });
            }
        }

        [HttpGet]
        [Authorize]
        [Route("hospitalId={hospitalId}")]
        public async Task<ActionResult<GetPatientsByHospitalIdResponseModel>> GetPatientsByHospitalIdAsync(Guid hospitalId)
        {
            GetPatientsByHospitalIdResponseModel result = new();
            try
            {
                if (hospitalId == Guid.Empty)
                {
                    result.Success = false;
                    result.Message = "Invalid HospitalId provided.";
                }
                else
                {
                    GetPatientsByHospitalIdRequestModel request = new()
                    {
                        HospitalId = hospitalId
                    };
                    result = await _mediator.Send(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching patients for HospitalId: {HospitalId}", hospitalId);
                result.Success = false;
                result.Message = "An error occurred while processing your request.";
            }

            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("analysis/hospitalId={hospitalId}&patientId={patientId}")]
        public async Task<ActionResult<GetPatientAnalysisResponseModel>> GetPatientDetailsByIdAsync(Guid hospitalId, string? patientId)
        {
            GetPatientAnalysisResponseModel result = new();
            try
            {
                if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId) || string.IsNullOrEmpty(patientId))
                {
                    result.Success = false;
                    result.Message = "Invalid HospitalId or PatientId provided.";
                }
                else
                {
                    GetPatientAnalysisRequestModel request = new()
                    {
                        HospitalId = hospitalId,
                        PatientId = patientId
                    };
                    result = await _mediator.Send(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching patient details for HospitalId: {HospitalId} and PatientId: {PatientId}", hospitalId, patientId);
                result.Success = false;
                result.Message = "An error occurred while processing your request.";
            }

            return Ok(result);
        }
        [Authorize]
        [HttpGet]
        [Route("visit-summary/appointmentId={appointmentId}")]
        public async Task<ActionResult<GetPatientVisitSummaryPdfResponseModel>> GetPatientVisitSummaryAsync(Guid appointmentId)
        {
            GetPatientVisitSummaryPdfResponseModel result = new();
            try
            {
                if (appointmentId == Guid.Empty)
                {
                    result.Success = false;
                    result.Message = "AppointmentId is required.";
                }
                else
                {
                    GetPatientVisitSummaryPdfRequestModel request = new()
                    {
                        AppointmentId = appointmentId
                    };
                    result = await _mediator.Send(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching visit summary for AppointmentId: {appointmentId}", appointmentId);
                result.Success = false;
                result.Message = "An error occurred while processing your request.";
            }

            return Ok(result);
        }
    }
}
