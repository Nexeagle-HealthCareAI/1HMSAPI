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
    // Discharge-summary letterhead: upload a PDF template, set margins/typography/overflow.
    // Mirrors PrescriptionSettingsController.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("discharge-settings")]
    public class DischargeSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DischargeSettingsController> _logger;

        public DischargeSettingsController(IMediator mediator, ILogger<DischargeSettingsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<GetDischargeSettingsResponseModel>> GetDischargeSettings(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetDischargeSettings started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            try
            {
                if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                GetDischargeSettingsRequestModel request = new()
                {
                    DoctorId = doctorId,
                    HospitalId = hospitalId
                };

                var result = await _mediator.Send(request);
                _logger.LogInformation("GetDischargeSettings ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", doctorId, hospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDischargeSettings for doctorId: {DoctorId} & hospitalId: {HospitalId}", doctorId, hospitalId);
                return StatusCode(500, new { Message = "An error occurred while retrieving discharge settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<ActionResult<UpdateDischargeSettingsResponseModel>> UpdateDischargeSettings(UpdateDischargeSettingsRequestModel request)
        {
            _logger.LogInformation("UpdateDischargeSettings started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, request.DoctorId, request.HospitalId);
            try
            {
                if (request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.LoggedInUserId = userId;

                    if (request.LoggedInUserId == Guid.Empty)
                    {
                        return BadRequest(new { Message = "LoggedInUserId is required and cannot be empty." });
                    }
                }

                var result = await _mediator.Send(request);
                _logger.LogInformation("UpdateDischargeSettings ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDischargeSettings for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating discharge settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("upload-template")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<UploadDischargeTemplateResponseModel>> UploadDischargeTemplate([FromForm] UploadDischargeTemplateRequestModel request)
        {
            _logger.LogInformation("UploadDischargeTemplate started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, request.DoctorId, request.HospitalId);
            try
            {
                if (request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.LoggedInUserId = userId;

                    if (request.LoggedInUserId == Guid.Empty)
                    {
                        return BadRequest(new { Message = "LoggedInUserId is required and cannot be empty." });
                    }
                }

                var result = await _mediator.Send(request);
                _logger.LogInformation("UploadDischargeTemplate ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadDischargeTemplate for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while uploading the discharge letterhead template", Error = ex.Message });
            }
        }
    }
}
