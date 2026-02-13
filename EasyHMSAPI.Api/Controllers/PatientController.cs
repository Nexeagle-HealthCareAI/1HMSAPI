using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyHMSAPI.Api.Controllers
{
    [Route("patient")]
    [ApiController]
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
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
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
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
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
                result.Message = "An error occurred while processing your request." + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
    }
}
