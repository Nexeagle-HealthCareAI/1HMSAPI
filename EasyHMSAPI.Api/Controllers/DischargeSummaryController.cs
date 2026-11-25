using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
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
    [RequiresPermission("ipd")]
    public class DischargeSummaryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DischargeSummaryController> _logger;

        public DischargeSummaryController(IMediator mediator, ILogger<DischargeSummaryController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Values of the custom (hospital/doctor-defined) discharge fields for one admission.
        [HttpGet("custom-fields")]
        public async Task<ActionResult<DischargeCustomFieldsResponseModel>> GetCustomFields([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });
            try
            {
                return Ok(await _mediator.Send(new GetDischargeCustomFieldsRequestModel { HospitalId = hospitalId, AdmissionId = admissionId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCustomFields for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while loading the custom discharge fields." });
            }
        }

        [HttpPut("custom-fields")]
        public async Task<ActionResult<DischargeCustomFieldsResponseModel>> SaveCustomFields([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId, [FromBody] Dictionary<string, string> values)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });
            try
            {
                var response = await _mediator.Send(new SaveDischargeCustomFieldsRequestModel
                {
                    HospitalId = hospitalId,
                    AdmissionId = admissionId,
                    UpdatedBy = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext),
                    Values = values ?? new Dictionary<string, string>(),
                });
                return response.Success ? Ok(response) : BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveCustomFields for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while saving the custom discharge fields." });
            }
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

        // Ready-to-print PNG (NexEagle logo centered) encoding this discharge summary's
        // WhatsApp-delivery link -- scanning it lands the patient in WhatsApp, where the bot
        // sends the actual PDF back. Mints AccessToken on first call if not already set.
        [HttpGet("qr-code")]
        public async Task<IActionResult> GetQrCode([FromQuery] Guid hospitalId, [FromQuery] Guid admissionId)
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });

            try
            {
                var response = await _mediator.Send(new GetDischargeSummaryQrCodeRequestModel { HospitalId = hospitalId, AdmissionId = admissionId });
                if (!response.Success || response.Content == null) return BadRequest(new { response.Message });
                return File(response.Content, response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetQrCode for admissionId: {AdmissionId}", admissionId);
                return StatusCode(500, new { Message = "An error occurred while generating the QR code." });
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
                request.LoggedInUserId = UserContextHelper.GetUserId(HttpContext.User);
                var response = await _mediator.Send(request);
                if (response.Forbidden)
                    return StatusCode(403, new { response.Message });
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

        // Issues a new public link/QR token (old one stops working immediately) and restarts its validity.
        [HttpPost("regenerate-link")]
        public async Task<ActionResult<RegenerateDischargeLinkResponseModel>> RegenerateLink([FromBody] RegenerateDischargeLinkRequestModel request)
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
                _logger.LogError(ex, "Error in RegenerateLink for admissionId: {AdmissionId}", request.AdmissionId);
                return StatusCode(500, new { Message = "An error occurred while regenerating the discharge summary link." });
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

        // ── AI help (Groq) ─────────────────────────────────────────────────────────────────
        // Review-and-apply: none of these save anything. Each re-reads the stay's records on the
        // server and de-identifies text before it is sent to the AI provider.

        private async Task<ActionResult<T>> RunAiAsync<T>(Func<Guid, Task<T>> send, Guid hospitalId, Guid admissionId, string what)
            where T : class
        {
            if (hospitalId == Guid.Empty || admissionId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionId are required." });
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                return Ok(await send(userId.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in discharge AI {What} for admissionId: {AdmissionId}", what, admissionId);
                return StatusCode(500, new { Message = "An error occurred while using AI help. Please try again." });
            }
        }

        // Draft the narrative sections from the stay's records.
        [HttpPost("ai/draft")]
        public Task<ActionResult<DraftDischargeSectionsResponseModel>> AiDraft([FromBody] DraftDischargeSectionsRequestModel request) =>
            RunAiAsync(uid => { request.CallerUserId = uid; return _mediator.Send(request); }, request.HospitalId, request.AdmissionId, "draft");

        // Dictation (audio) → transcript → sorted into sections + medicine-change suggestions.
        [HttpPost("ai/dictate")]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public Task<ActionResult<DictateDischargeResponseModel>> AiDictate([FromForm] DictateDischargeRequestModel request) =>
            RunAiAsync(uid => { request.CallerUserId = uid; return _mediator.Send(request); }, request.HospitalId, request.AdmissionId, "dictate");

        // Rewrite one section: SHORTER / DETAIL / PATIENT.
        [HttpPost("ai/rewrite")]
        public Task<ActionResult<RewriteDischargeSectionResponseModel>> AiRewrite([FromBody] RewriteDischargeSectionRequestModel request) =>
            RunAiAsync(uid => { request.CallerUserId = uid; return _mediator.Send(request); }, request.HospitalId, request.AdmissionId, "rewrite");

        // Translate the patient-advice sections into the hospital's second print language.
        [HttpPost("ai/translate-advice")]
        public Task<ActionResult<TranslateDischargeAdviceResponseModel>> AiTranslateAdvice([FromBody] TranslateDischargeAdviceRequestModel request) =>
            RunAiAsync(uid => { request.CallerUserId = uid; return _mediator.Send(request); }, request.HospitalId, request.AdmissionId, "translate");

        // Hospital-wide discharge choices: second print language and the AI switch.
        [HttpGet("hospital-preferences")]
        public async Task<ActionResult<HospitalDischargePreferencesResponseModel>> GetHospitalPreferences([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
            try
            {
                return Ok(await _mediator.Send(new GetHospitalDischargePreferencesRequestModel { HospitalId = hospitalId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetHospitalPreferences for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while loading the discharge settings." });
            }
        }

        [HttpPut("hospital-preferences")]
        [RequiresPermission("admin_panel")]
        public async Task<ActionResult<HospitalDischargePreferencesResponseModel>> UpdateHospitalPreferences([FromBody] UpdateHospitalDischargePreferencesRequestModel request)
        {
            if (request.HospitalId == Guid.Empty) return BadRequest(new { Message = "hospitalId is required." });
            var userId = UserContextHelper.GetUserId(HttpContext.User);
            if (userId == null) return Unauthorized(new { Message = "Could not resolve the signed-in user." });
            try
            {
                request.CallerUserId = userId.Value;
                var response = await _mediator.Send(request);
                return response.Success ? Ok(response) : BadRequest(new { response.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateHospitalPreferences for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while saving the discharge settings." });
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
