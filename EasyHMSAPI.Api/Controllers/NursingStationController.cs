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
    [Route("nursing-station")]
    [Authorize]
    [RequiresPermission("nursing_station")]
    public class NursingStationController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NursingStationController> _logger;

        public NursingStationController(IMediator mediator, ILogger<NursingStationController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("assignment")]
        public async Task<ActionResult<AssignNurseShiftResponseModel>> AssignNurseShift([FromBody] AssignNurseShiftRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.NurseUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.WardCode))
                return BadRequest(new { Message = "hospitalId, nurseUserId and wardCode are required." });

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
                _logger.LogError(ex, "Error in AssignNurseShift for hospitalId: {HospitalId}, nurseUserId: {NurseUserId}", request.HospitalId, request.NurseUserId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("assignment/release")]
        public async Task<ActionResult<ReleaseNurseShiftResponseModel>> ReleaseNurseShift([FromBody] ReleaseNurseShiftRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.NurseShiftAssignmentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and nurseShiftAssignmentId are required." });

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
                _logger.LogError(ex, "Error in ReleaseNurseShift for assignmentId: {AssignmentId}", request.NurseShiftAssignmentId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("roster")]
        public async Task<ActionResult<GetNurseRosterResponseModel>> GetRoster(
            [FromQuery] Guid hospitalId, [FromQuery] string? wardCode, [FromQuery] string? shiftCode,
            [FromQuery] Guid? nurseUserId, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetNurseRosterRequestModel
                {
                    HospitalId = hospitalId,
                    WardCode = wardCode,
                    ShiftCode = shiftCode,
                    NurseUserId = nurseUserId,
                    ActiveOnly = activeOnly,
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRoster for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // "My patients": every active admission on a ward this nurse (or the one specified) is
        // currently rostered to, with last vitals and MAR due/overdue folded in. nurseUserId
        // defaults to the caller so the station works with zero query params for the common case.
        [HttpGet("summary")]
        public async Task<ActionResult<GetNursingStationSummaryResponseModel>> GetSummary(
            [FromQuery] Guid hospitalId, [FromQuery] Guid? nurseUserId, [FromQuery] string? wardCode, [FromQuery] string? shiftCode)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetNursingStationSummaryRequestModel
                {
                    HospitalId = hospitalId,
                    NurseUserId = nurseUserId ?? UserContextHelper.GetUserId(HttpContext.User),
                    WardCode = wardCode,
                    ShiftCode = shiftCode,
                };
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSummary for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        // Per-patient nurse assignment -- independent of the ward roster above (a nurse doesn't
        // need to already be rostered to the ward to be assigned to a specific patient).
        [HttpPost("patient-assignment")]
        public async Task<ActionResult<AssignPatientNurseResponseModel>> AssignPatientNurse([FromBody] AssignPatientNurseRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.NurseUserId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, nurseUserId and admissionId are required." });

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
                _logger.LogError(ex, "Error in AssignPatientNurse for hospitalId: {HospitalId}, admissionId: {AdmissionId}", request.HospitalId, request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpPost("patient-assignment/release")]
        public async Task<ActionResult<ReleasePatientNurseResponseModel>> ReleasePatientNurse([FromBody] ReleasePatientNurseRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.PatientNurseAssignmentId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and patientNurseAssignmentId are required." });

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
                _logger.LogError(ex, "Error in ReleasePatientNurse for assignmentId: {AssignmentId}", request.PatientNurseAssignmentId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("patient-assignments")]
        public async Task<ActionResult<GetPatientNurseAssignmentsResponseModel>> GetPatientAssignments(
            [FromQuery] Guid hospitalId, [FromQuery] Guid admissionId, [FromQuery] bool activeOnly = true)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetPatientNurseAssignmentsRequestModel
                {
                    HospitalId = hospitalId,
                    AdmissionId = admissionId,
                    ActiveOnly = activeOnly,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientAssignments for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }

        [HttpGet("nurses")]
        public async Task<ActionResult<GetHospitalNursesResponseModel>> GetNurses([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var response = await _mediator.Send(new GetHospitalNursesRequestModel { HospitalId = hospitalId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNurses for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred." });
            }
        }
    }
}
