using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("patient-profile")]
    public class PatientProfileController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PatientProfileController> _logger;
        public PatientProfileController(IMediator mediator, ILogger<PatientProfileController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<GetPatientProfileResponseModel>> GetPatientProfile([FromQuery] Guid hospitalId, [FromQuery] string patientId)
        {
            _logger.LogInformation("GetPatientProfile started at {Time} for hospitalId: {HospitalId}, patientId: {PatientId}", DateTime.UtcNow, hospitalId, patientId);
            try
            {
                if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId))
                    return BadRequest(new { Message = "hospitalId and patientId are required." });

                var request = new GetPatientProfileRequestModel { HospitalId = hospitalId, PatientId = patientId };
                var response = await _mediator.Send(request);
                if (response == null)
                    return NotFound(new { Message = "Patient not found." });
                _logger.LogInformation("GetPatientProfile ended for hospitalId: {HospitalId}, patientId: {PatientId}", hospitalId, patientId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientProfile for hospitalId: {HospitalId}, patientId: {PatientId}", hospitalId, patientId);
                return StatusCode(500, new { Message = "An error occurred while retrieving patient profile", Error = ex.Message });
            }
        }

        [HttpPut]
        [Authorize]
        public async Task<ActionResult<UpdatePatientProfileResponseModel>> UpdatePatientProfile([FromQuery] Guid hospitalId, [FromQuery] string patientId, [FromBody] UpdatePatientProfileRequestModel request)
        {
            _logger.LogInformation("UpdatePatientProfile started at {Time} for hospitalId: {HospitalId}, patientId: {PatientId}", DateTime.UtcNow, hospitalId, patientId);
            try
            {
                if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId))
                    return BadRequest(new { Message = "hospitalId and patientId are required." });

                request.HospitalId = hospitalId;
                request.PatientId = patientId;
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(response);
                _logger.LogInformation("UpdatePatientProfile ended for hospitalId: {HospitalId}, patientId: {PatientId}", hospitalId, patientId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePatientProfile for hospitalId: {HospitalId}, patientId: {PatientId}", hospitalId, patientId);
                return StatusCode(500, new { Message = "An error occurred while updating patient profile", Error = ex.Message });
            }
        }
    }
}
