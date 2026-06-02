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
    }
}
