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

        // Admit a patient for an existing billing encounter (starts the IPD stay).
        [HttpPost]
        public async Task<ActionResult<AdmitPatientResponseModel>> Admit([FromBody] AdmitPatientRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
                return BadRequest(new { Message = "hospitalId, patientId and encounterId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Admit for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while admitting the patient." });
            }
        }
    }
}
