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
    [Route("prescription-settings")]
    public class PrescriptionSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PrescriptionSettingsController> _logger;

        public PrescriptionSettingsController(IMediator mediator, ILogger<PrescriptionSettingsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<GetPrescriptionSettingsResponseModel>> GetPrescriptionSettings(Guid doctorId, Guid hospitalId)
        {
            _logger.LogInformation("GetPrescriptionSettings started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, doctorId, hospitalId);
            try
            {
                if (doctorId == Guid.Empty || hospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                GetPrescriptionSettingsRequestModel request = new()
                {
                    DoctorId = doctorId,
                    HospitalId = hospitalId
                };

                var result = await _mediator.Send(request);
                _logger.LogInformation("GetPrescriptionSettings ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", doctorId, hospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPrescriptionSettings for doctorId: {DoctorId} & hospitalId: {HospitalId}", doctorId, hospitalId);

                return StatusCode(500, new { Message = "An error occurred while retrieving prescription settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<ActionResult<UpdatePrescriptionSettingsResponseModel>> UpdatePrescriptionSettings(UpdatePrescriptionSettingsRequestModel request)
        {
            _logger.LogInformation("UpdatePrescriptionSettings started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, request.DoctorId, request.HospitalId);
            try
            {
                if(request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                var userIdClaim = User.FindFirst("userId")?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.LoggedInUserId = userId;

                    if(request.LoggedInUserId == Guid.Empty)
                    {
                        return BadRequest(new { Message = "LoggedInUserId is required and cannot be empty." });
                    }
                }

                var result = await _mediator.Send(request);
                _logger.LogInformation("UpdatePrescriptionSettings ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePrescriptionSettings for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);

                return StatusCode(500, new { Message = "An error occurred while updating prescription settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("upload-template")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<UploadPrescriptionAttachmentsResponseModel>> UploadPrescriptionTemplate([FromForm] UploadPrescriptionTemplateRequestModel request)
        {
            _logger.LogInformation("UploadAsset started at {Time} for doctorId: {DoctorId} & hospitalId: {HospitalId}", DateTime.UtcNow, request.DoctorId, request.HospitalId);
            try
            {
                if (request.DoctorId == Guid.Empty || request.HospitalId == Guid.Empty)
                {
                    return BadRequest(new { Message = "DoctorId and HospitalId are required and cannot be empty." });
                }

                var userIdClaim = User.FindFirst("userId")?.Value;
                Guid loggedInUserId = Guid.Empty;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.LoggedInUserId = userId;

                    if (request.LoggedInUserId == Guid.Empty)
                    {
                        return BadRequest(new { Message = "LoggedInUserId is required and cannot be empty." });
                    }
                }

                var result = await _mediator.Send(request);
                _logger.LogInformation("UploadAsset ended for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadAsset for doctorId: {DoctorId} & hospitalId: {HospitalId}", request.DoctorId, request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while uploading asset", Error = ex.Message });
            }
        }
    }
}
