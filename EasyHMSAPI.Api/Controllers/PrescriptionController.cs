using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("prescription")]
    public class PrescriptionController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PrescriptionController> _logger;

        public PrescriptionController(IMediator mediator, ILogger<PrescriptionController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("prescription-settings")]
        public async Task<IActionResult> GetPrescriptionSettings(Guid doctorId)
        {
            _logger.LogInformation("GetPrescriptionSettings started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, doctorId);
            try
            {
                var request = new GetPrescriptionSettingsRequestModel { DoctorId = doctorId };
                var result = await _mediator.Send(request);
                _logger.LogInformation("GetPrescriptionSettings ended for doctorId: {DoctorId}", doctorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPrescriptionSettings for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while retrieving prescription settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("prescription-settings")]
        public async Task<IActionResult> UpdatePrescriptionSettings(UpdatePrescriptionSettingsRequestModel request)
        {
            _logger.LogInformation("UpdatePrescriptionSettings started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, request.DoctorId);
            try
            {
                var result = await _mediator.Send(request);
                _logger.LogInformation("UpdatePrescriptionSettings ended for doctorId: {DoctorId}", request.DoctorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePrescriptionSettings for doctorId: {DoctorId}", request.DoctorId);
                return StatusCode(500, new { Message = "An error occurred while updating prescription settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("prescription-settings/reset")]
        public async Task<IActionResult> ResetPrescriptionSettings(Guid doctorId)
        {
            _logger.LogInformation("ResetPrescriptionSettings started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, doctorId);
            try
            {
                var request = new ResetPrescriptionSettingsRequestModel { DoctorId = doctorId };
                var result = await _mediator.Send(request);
                _logger.LogInformation("ResetPrescriptionSettings ended for doctorId: {DoctorId}", doctorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPrescriptionSettings for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while resetting prescription settings", Error = ex.Message });
            }
        }

        [HttpPost("assets/upload")]
        [Authorize]
        public async Task<IActionResult> UploadAsset([FromForm] UploadAssetRequestModel request)
        {
            _logger.LogInformation("UploadAsset started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, request.DoctorId);
            try
            {
                List<string> permittedAssetTypes = new()
                {
                    AppConstants.AssetType_HeaderImage,
                    AppConstants.AssetType_FooterImage,
                    AppConstants.AssetType_SignatureImage
                };

                if (string.IsNullOrWhiteSpace(request.AssetType) || !permittedAssetTypes.Contains(request.AssetType.ToLower()))
                {
                    return BadRequest(new { Success = false, Message = "Invalid asset type." });
                }

                var result = await _mediator.Send(request);
                _logger.LogInformation("UploadAsset ended for doctorId: {DoctorId}", request.DoctorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadAsset for doctorId: {DoctorId}", request.DoctorId);
                return StatusCode(500, new { Message = "An error occurred while uploading asset", Error = ex.Message });
            }
        }

        [HttpGet("assets")]
        [Authorize]
        public async Task<IActionResult> GetAssets(Guid doctorId)
        {
            _logger.LogInformation("GetAssets started at {Time} for doctorId: {DoctorId}", DateTime.UtcNow, doctorId);
            try
            {
                var request = new GetAssetsRequestModel { DoctorId = doctorId };
                var result = await _mediator.Send(request);
                _logger.LogInformation("GetAssets ended for doctorId: {DoctorId}", doctorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAssets for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while retrieving assets", Error = ex.Message });
            }
        }

        [HttpDelete("assets/remove")]
        [Authorize]
        public async Task<IActionResult> DeleteAsset(DeleteAssetRequestModel request)
        {
            _logger.LogInformation("DeleteAsset started at {Time}", DateTime.UtcNow);
            try
            {
                var result = await _mediator.Send(request);
                _logger.LogInformation("DeleteAsset ended");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteAsset");
                return StatusCode(500, new { Message = "An error occurred while deleting asset", Error = ex.Message });
            }
        }
    }
}
