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
    // Discharge summary — compose draft, save, sign, AI-assist narrative.
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("discharge-summary")]
    [Authorize]
    public class DischargeSummaryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DischargeSummaryController> _logger;

        public DischargeSummaryController(IMediator mediator, ILogger<DischargeSummaryController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("draft")]
        public async Task<ActionResult<GetDischargeSummaryDraftResponseModel>> GetDraft([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetDischargeSummaryDraftRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDraft for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while composing the discharge summary draft." });
            }
        }

        [HttpPut]
        public async Task<ActionResult<SaveDischargeSummaryResponseModel>> Save([FromBody] SaveDischargeSummaryRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Save for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while saving the discharge summary." });
            }
        }

        [HttpPost("sign")]
        public async Task<ActionResult<SignDischargeSummaryResponseModel>> Sign([FromBody] SignDischargeSummaryRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Sign for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while signing the discharge summary." });
            }
        }

        [HttpPost("unsign")]
        public async Task<ActionResult<UnsignDischargeSummaryResponseModel>> Unsign([FromBody] UnsignDischargeSummaryRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

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
                _logger.LogError(ex, "Error in Unsign for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while unsigning the discharge summary." });
            }
        }

        // Uploads the client-rendered discharge PDF for the QR "view anytime" link + WhatsApp send.
        [HttpPost("upload-pdf")]
        public async Task<ActionResult<UploadDischargeSummaryPdfResponseModel>> UploadPdf(UploadDischargeSummaryPdfRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadPdf for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while uploading the discharge summary PDF." });
            }
        }

        [HttpPost("send-whatsapp")]
        public async Task<ActionResult<SendDischargeSummaryWhatsAppResponseModel>> SendWhatsApp([FromBody] SendDischargeSummaryWhatsAppRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendWhatsApp for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while sending the discharge summary via WhatsApp." });
            }
        }

        [HttpPost("narrate")]
        public async Task<ActionResult<GenerateDischargeNarrativeResponseModel>> Narrate([FromBody] GenerateDischargeNarrativeRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });

            try
            {
                request.CallerUserId = userId.Value;
                var response = await _mediator.Send(request);
                if (!response.Success)
                    return BadRequest(new { response.Message });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Narrate for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while generating the narrative." });
            }
        }

        // Personalized discharge-summary field layout (global per doctor): rename / reorder /
        // show-hide built-in fields and add custom fields. Mirrors EPrescriptionController's
        // configuration/field-layout routes — scoped per (doctorId, hospitalId).
        [HttpGet("configuration/field-layout/doctorId={doctorId}")]
        public async Task<ActionResult<GetDoctorDischargeFieldConfigResponseModel>> GetFieldLayout(Guid doctorId, [FromQuery] Guid hospitalId)
        {
            if (doctorId == Guid.Empty)
                return BadRequest(new { Message = "Invalid doctorId." });

            var result = await _mediator.Send(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = doctorId, HospitalId = hospitalId });
            return Ok(result);
        }

        [HttpPut("configuration/field-layout/doctorId={doctorId}")]
        public async Task<ActionResult<UpdateDoctorDischargeFieldConfigResponseModel>> UpdateFieldLayout(Guid doctorId, [FromQuery] Guid hospitalId, [FromBody] UpdateDoctorDischargeFieldConfigRequestModel model)
        {
            if (model == null)
                return BadRequest(new { Message = "Invalid request body." });
            if (doctorId == Guid.Empty)
                return BadRequest(new { Message = "Invalid doctorId." });

            model.DoctorId = doctorId;
            model.HospitalId = hospitalId;
            var result = await _mediator.Send(model);
            return Ok(result);
        }
    }
}
