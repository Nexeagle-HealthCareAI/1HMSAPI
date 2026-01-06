using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Esf;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AppointmentsController> _logger;
        public AppointmentsController(IMediator mediator, ILogger<AppointmentsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("departments")]
        [Authorize]
        public async Task<IActionResult> GetDepartments([FromQuery] Guid hospitalId)
        {
            _logger.LogInformation("GetDepartments started at {Time} for hospitalId: {HospitalId}", DateTime.UtcNow, hospitalId);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            var request = new GetAppointmentDepartmentsRequestModel { HospitalId = hospitalId };
            var response = await _mediator.Send(request);
            _logger.LogInformation("GetDepartments ended for hospitalId: {HospitalId}", hospitalId);

            return Ok(response);
        }

        [HttpGet("department-doctor")]
        [Authorize]
        public async Task<IActionResult> GetDepartmentDoctors([FromQuery] Guid departmentId, [FromQuery] Guid hospitalId)
        {
            _logger.LogInformation("GetDepartmentDoctors started at {Time} for departmentId: {DepartmentId}, hospitalId: {HospitalId}", DateTime.UtcNow, departmentId, hospitalId);
            if (departmentId == Guid.Empty)
                return BadRequest(new { Message = "departmentId is required." });
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            var request = new GetDepartmentDoctorsRequestModel { DepartmentId = departmentId, HospitalId = hospitalId };
            var response = await _mediator.Send(request);
            _logger.LogInformation("GetDepartmentDoctors ended for departmentId: {DepartmentId}, hospitalId: {HospitalId}", departmentId, hospitalId);

            return Ok(response);
        }

        [HttpPost("register/{hospitalId}")]
        [Authorize]
        public async Task<IActionResult> RegisterAppointment([FromRoute] Guid hospitalId, [FromQuery] bool allocateToken, [FromBody] RegisterAppointmentRequestModel request)
        {
            _logger.LogInformation("RegisterAppointment started at {Time} for hospitalId: {HospitalId}, allocateToken: {AllocateToken}", DateTime.UtcNow, hospitalId, allocateToken);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "Hospital ID is required." });

            // Assign UserId from token if present
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                request.UserId = userId;
            }

            if (request.UserId == Guid.Empty)
                return BadRequest(new { Message = "User ID is required." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                request.HospitalId = hospitalId;
                request.AllocateToken = allocateToken;
                var response = await _mediator.Send(request);
                _logger.LogInformation("RegisterAppointment successful for hospitalId: {HospitalId}, UserId: {UserId}", hospitalId, request.UserId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterAppointment for hospitalId: {HospitalId}, UserId: {UserId}", hospitalId, request.UserId);
                return StatusCode(500, new { ex.Message });
            }
        }

        [HttpGet("patient-details/search")]
        [Authorize]
        public async Task<IActionResult> SearchPatient([FromQuery] string by, [FromQuery] string q, [FromQuery] Guid hospitalId, [FromQuery] string scope = "local")
        {
            _logger.LogInformation("SearchPatient started at {Time} with parameters - by: {By}, q: {Q}, hospitalId: {HospitalId}, scope: {Scope}", DateTime.UtcNow, by, q, hospitalId, scope);
            if (string.IsNullOrWhiteSpace(by) || string.IsNullOrWhiteSpace(q))
                return BadRequest(new { Message = "Search type (by) and query (q) parameters are required." });
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new SearchPatientRequestModel
                {
                    By = by.ToLower(),
                    Q = q,
                    Scope = scope,
                    HospitalId = hospitalId
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("SearchPatient ended successfully for query: {Q}, hospitalId: {HospitalId}", q, hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchPatient for query: {Q}, hospitalId: {HospitalId}", q, hospitalId);
                return StatusCode(500, new { ex.Message });
            }
        }

        [HttpPut("patient-status")]
        [Authorize]
        public async Task<IActionResult> UpdatePatientStatus([FromBody] UpdatePatientStatusRequestModel request)
        {
            _logger.LogInformation("UpdatePatientStatus started at {Time} for UserId: {UserId}", DateTime.UtcNow, request.UserId);
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst("userId")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                request.UserId = userId;
            }

            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdatePatientStatus successful for UserId: {UserId}", request.UserId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePatientStatus for UserId: {UserId}", request.UserId);
                return StatusCode(500, new { Message = $"Error updating patient status: {ex.Message}" });
            }
        }

        [HttpPost("patient-vitals")]
        [Authorize]
        public async Task<IActionResult> UpdatePatientVitals([FromBody] UpdatePatientVitalsRequestModel request)
        {
            _logger.LogInformation("UpdatePatientVitals started at {Time} for UserId: {UserId}", DateTime.UtcNow, request.RecordedBy);
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (request.RecordedBy == Guid.Empty)
                {
                    var userIdClaim = User.FindFirst("userId")?.Value;
                    if (Guid.TryParse(userIdClaim, out var userId))
                    {
                        request.RecordedBy = userId;
                    }
                    else
                    {
                        return BadRequest(new { Message = "Could not determine the current user. Please provide recordedBy." });
                    }
                }

                var response = await _mediator.Send(request);
                _logger.LogInformation("UpdatePatientVitals successful for RecordedBy: {RecordedBy}", request.RecordedBy);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePatientVitals for RecordedBy: {RecordedBy}", request.RecordedBy);
                return StatusCode(500, new { Message = $"Error updating patient vitals: {ex.Message}" });
            }
        }

        [HttpPost("patient-reschedule")]
        [Authorize]
        public async Task<IActionResult> RescheduleAppointment([FromBody] RescheduleAppointmentRequestModel request)
        {
            _logger.LogInformation("RescheduleAppointment started at {Time} for AppointmentId: {AppointmentId}", DateTime.UtcNow, request.AppointmentId);
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Validate that the reschedule date is in the future
                if (request.ToApptDate.Date <= DateTime.Today)
                {
                    _logger.LogWarning("RescheduleAppointment called with a past date for AppointmentId: {AppointmentId}", request.AppointmentId);
                    return BadRequest(new { Message = "Reschedule date must be in the future." });
                }

                var response = await _mediator.Send(request);
                _logger.LogInformation("RescheduleAppointment successful for AppointmentId: {AppointmentId}", request.AppointmentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RescheduleAppointment for AppointmentId: {AppointmentId}", request.AppointmentId);
                return StatusCode(500, new { Message = $"Error rescheduling appointment: {ex.Message}" });
            }
        }

        [HttpPatch("patient-cancel")]
        [Authorize]
        public async Task<IActionResult> CancelAppointment([FromBody] CancelAppointmentRequestModel request)
        {
            _logger.LogInformation("CancelAppointment started at {Time} for AppointmentId: {AppointmentId}", DateTime.UtcNow, request.AppointmentId);
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var response = await _mediator.Send(request);
                _logger.LogInformation("CancelAppointment successful for AppointmentId: {AppointmentId}", request.AppointmentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CancelAppointment for AppointmentId: {AppointmentId}", request.AppointmentId);
                return StatusCode(500, new { Message = $"Error canceling appointment: {ex.Message}" });
            }
        }

        [HttpGet("patient-appointment-details")]
        [Authorize]
        public async Task<IActionResult> GetPatientAppointmentDetails(
            [FromQuery] string? status,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid? doctorId)
        {
            _logger.LogInformation("GetPatientAppointmentDetails started at {Time} for hospitalId: {HospitalId}, status: {Status}, startDate: {StartDate}, endDate: {EndDate}", DateTime.UtcNow, hospitalId, status, startDate, endDate);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "HospitalId is required." });

            try
            {
                var request = new GetPatientAppointmentDetailsRequestModel
                {
                    Status = status,
                    StartDate = startDate,
                    EndDate = endDate,
                    HospitalId = hospitalId,
                    DoctorId = doctorId
                };

                var response = await _mediator.Send(request);
                _logger.LogInformation("GetPatientAppointmentDetails ended for hospitalId: {HospitalId}", hospitalId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientAppointmentDetails for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = $"Error fetching appointment details: {ex.Message}" });
            }
        }

        [HttpGet("patient-booked-slots")]
        [Authorize]
        public async Task<IActionResult> GetPatientBookedSlots([FromQuery] Guid doctorId, [FromQuery] Guid hospitalId, [FromQuery] DateTime date)
        {
            _logger.LogInformation("GetPatientBookedSlots started at {Time} for doctorId: {DoctorId}, hospitalId: {HospitalId}, date: {Date}", DateTime.UtcNow, doctorId, hospitalId, date);
            if (doctorId == Guid.Empty)
                return BadRequest(new { Message = "doctorId is required." });
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (date == default)
                return BadRequest(new { Message = "date is required." });

            var request = new DoctorBookedSlotsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                Date = date
            };
            var response = await _mediator.Send(request);
            _logger.LogInformation("GetPatientBookedSlots ended for doctorId: {DoctorId}, hospitalId: {HospitalId}", doctorId, hospitalId);

            return Ok(response);
        }

        [HttpGet("hospital-kpi-matrix")]
        [Authorize]
        public async Task<IActionResult> GetHospitalKpiMatrix([FromQuery] Guid hospitalId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] Guid? doctorId)
        {
            _logger.LogInformation("GetHospitalKpiMatrix started at {Time} for hospitalId: {HospitalId}, startDate: {StartDate}, endDate: {EndDate}", DateTime.UtcNow, hospitalId, startDate, endDate);
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            var request = new HospitalKpiMatrixRequestModel
            {
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate,
                DoctorId = doctorId
            };
            var response = await _mediator.Send(request);
            _logger.LogInformation("GetHospitalKpiMatrix ended for hospitalId: {HospitalId}", hospitalId);

            return Ok(response);
        }

        [HttpPost("complete-appointment")]
        [Authorize]
        public async Task<ActionResult<CompleteAppointmentResponseModel>> CompleteAppointment([FromBody] CompleteAppointmentRequestModel request)
        {
            _logger.LogInformation("CompleteAppointment started at {Time} for AppointmentId: {AppointmentId}", DateTime.UtcNow, request.AppointmentId);
            CompleteAppointmentResponseModel result = new();
            try
            {
                if (request.HospitalId == Guid.Empty || request.DoctordId == Guid.Empty || request.AppointmentId == Guid.Empty || string.IsNullOrEmpty(request.PatientId) || string.IsNullOrWhiteSpace(request.PatientId))
                {
                    result.Success = false;
                    result.Message = "HospitalId, DoctordId, AppointmentId, and PatientId are required.";
                }
                else
                {
                    result = await _mediator.Send(request);
                    _logger.LogInformation("CompleteAppointment successful for AppointmentId: {AppointmentId}", request.AppointmentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CompleteAppointment for AppointmentId: {AppointmentId}", request.AppointmentId);
                result.Success = false;
                result.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return Ok(result);
        }
    }
}
