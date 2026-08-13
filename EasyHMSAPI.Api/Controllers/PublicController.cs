using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Public surface for external integrations (the Nexeagle booking website and, generically,
    /// any site wanting to list/book/review publicly-listed doctors). No staff JWT — the
    /// X-Api-Key header is optional (see PublicApiKeyFilter): anonymous callers are let through,
    /// a header is only needed if a consumer wants its traffic identified/revocable. Not scoped
    /// to one hospital: GetDoctors returns every publicly-listed hospital's doctors, and
    /// GetDoctorAvailability/BookAppointment resolve HospitalId from the doctor being acted on,
    /// never from the key or the request body.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("public")]
    [AllowAnonymous]
    [EasyHMSAPI.Api.Common.SkipHospitalAccessCheck]
    [ServiceFilter(typeof(PublicApiKeyFilter))]
    [EnableRateLimiting("PublicBookingPolicy")]
    public class PublicController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PublicController> _logger;
        private readonly IPatientTokenValidator _patientTokenValidator;
        private readonly string? _proxyForwardingSecret;

        public PublicController(IMediator mediator, ILogger<PublicController> logger, IPatientTokenValidator patientTokenValidator, IConfiguration configuration)
        {
            _mediator = mediator;
            _logger = logger;
            _patientTokenValidator = patientTokenValidator;
            _proxyForwardingSecret = configuration["Internal:ProxyForwardingSecret"];
        }

        // Page-view beacon for the CMS "Site Visits" report — fired on every NexEagleWebsite page
        // load. Deliberately its own (much more generous) rate-limit policy, overriding the
        // controller-level PublicBookingPolicy: a visitor browsing several pages would otherwise
        // exhaust that 20/min booking-abuse ceiling just from page navigation.
        [HttpPost("track-visit")]
        [EnableRateLimiting("TrackVisitPolicy")]
        public async Task<ActionResult<TrackVisitResponseModel>> TrackVisit([FromBody] TrackVisitRequestModel request)
        {
            try
            {
                request.IpAddress = EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(HttpContext, _proxyForwardingSecret);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.TrackVisit");
                // Never let visit tracking surface as a visible error to a site visitor.
                return Ok(new TrackVisitResponseModel { Success = false });
            }
        }

        // Generic funnel/behavior event beacon for the CMS Insights tab (Auth Funnel / Booking
        // Funnel / All Searches) — see AppConstants.AnalyticsEventType_* for valid EventType
        // values. Same generous rate-limit tier as TrackVisit: search/step events can fire several
        // times per page, well above the booking-abuse-oriented PublicBookingPolicy ceiling.
        [HttpPost("track-event")]
        [EnableRateLimiting("TrackVisitPolicy")]
        public async Task<ActionResult<TrackEventResponseModel>> TrackEvent([FromBody] TrackEventRequestModel request)
        {
            try
            {
                request.IpAddress = EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(HttpContext, _proxyForwardingSecret);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.TrackEvent");
                // Never let event tracking surface as a visible error to a site visitor.
                return Ok(new TrackEventResponseModel { Success = false });
            }
        }

        // Records one hospital-scoped marketing lead for the Lead Generation page -- see
        // AppConstants.LeadSource_*/LeadType_* for valid values. Called by both NexEagleWebsite
        // (doctor profile/hospital page views, name searches) and the WhatsApp bot (name
        // searches, resolved server-side via hms_client.record_lead). Same generous rate-limit
        // tier and error-swallowing posture as TrackEvent.
        [HttpPost("leads")]
        [EnableRateLimiting("TrackVisitPolicy")]
        public async Task<ActionResult<RecordLeadResponseModel>> RecordLead([FromBody] RecordLeadRequestModel request)
        {
            try
            {
                request.IpAddress = EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(HttpContext, _proxyForwardingSecret);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.RecordLead");
                return Ok(new RecordLeadResponseModel { Success = false });
            }
        }

        // Resolves a scanned OPD QR code to a hospital -- the bot gateway's GET /c/{hospital_code}
        // calls this to know which hospital's context to load. No auth beyond the controller's
        // optional X-Api-Key, same as every other endpoint here.
        [HttpGet("hospitals/by-code/{hospitalCode}")]
        public async Task<ActionResult<GetHospitalByCodeResponseModel>> GetHospitalByCode(string hospitalCode)
        {
            try
            {
                var response = await _mediator.Send(new GetHospitalByCodeRequestModel { HospitalCode = hospitalCode });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetHospitalByCode for hospitalCode: {HospitalCode}", hospitalCode);
                return StatusCode(500, new { Message = "An error occurred while resolving the hospital code." });
            }
        }

        // Platform-wide, publicly-listed hospitals -- e.g. the WhatsApp bot's new hospital-name
        // matching (resolver.match_hospital_by_query via hms_client.list_hospitals), which had
        // no bulk-listing endpoint to fuzzy-match against before this (only the exact single-code
        // lookup above).
        [HttpGet("hospitals")]
        public async Task<ActionResult<GetPublicHospitalsResponseModel>> GetHospitals()
        {
            try
            {
                var response = await _mediator.Send(new GetPublicHospitalsRequestModel());
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetHospitals");
                return StatusCode(500, new { Message = "An error occurred while fetching hospitals." });
            }
        }

        [HttpGet("doctors")]
        public async Task<ActionResult<GetPublicDoctorsResponseModel>> GetDoctors(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 24,
            [FromQuery] string? city = null, [FromQuery] string? state = null,
            [FromQuery] string? specialtyCategory = null, [FromQuery] string? search = null,
            [FromQuery] Guid? hospitalId = null)
        {
            try
            {
                var request = new GetPublicDoctorsRequestModel
                {
                    Page = page < 1 ? 1 : page,
                    PageSize = pageSize < 1 ? 24 : pageSize,
                    City = city,
                    State = state,
                    SpecialtyCategory = specialtyCategory,
                    Search = search,
                    HospitalId = hospitalId,
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctors");
                return StatusCode(500, new { Message = "An error occurred while fetching doctors." });
            }
        }

        [HttpGet("specialties")]
        public async Task<ActionResult<GetPublicSpecialtiesResponseModel>> GetSpecialties()
        {
            try
            {
                var response = await _mediator.Send(new GetPublicSpecialtiesRequestModel());
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetSpecialties");
                return StatusCode(500, new { Message = "An error occurred while fetching specialties." });
            }
        }

        // Single-doctor lookup -- previously there was no dedicated endpoint for this (see the
        // note in NexEagleWebsite's server.ts getDoctorById); used directly by the WhatsApp
        // bot's deterministic DRBOOK <doctorId> trigger (GET /doc/{doctorId} in webhook.py) to
        // resolve exactly one doctor, no name-matching involved.
        [HttpGet("doctors/{doctorId:guid}")]
        public async Task<ActionResult<GetPublicDoctorByIdResponseModel>> GetDoctorById(Guid doctorId)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorsRequestModel { DoctorId = doctorId, Page = 1, PageSize = 1 });
                var doctor = response.Doctors.FirstOrDefault();
                if (doctor == null)
                    return NotFound(new { Message = "Doctor not found." });
                return Ok(new GetPublicDoctorByIdResponseModel { Success = true, Doctor = doctor });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorById for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching the doctor." });
            }
        }

        // Doctor's own WhatsApp-booking QR (NexEagle logo centered) -- rendered on their Doctor
        // Dekho profile page. Scanning it lands the patient straight into a booking flow for
        // THIS exact doctor (skips specialty/name search entirely) -- see the bot's DRBOOK
        // trigger in conversation.py.
        [HttpGet("doctors/{doctorId:guid}/qr-code")]
        public async Task<IActionResult> GetDoctorQrCode(Guid doctorId)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorQrCodeRequestModel { DoctorId = doctorId });
                if (!response.Success || response.Content == null)
                    return NotFound(new { response.Message });
                return File(response.Content, response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorQrCode for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while generating the QR code." });
            }
        }

        // Generic "chat with us on WhatsApp" QR (NexEagle logo centered) -- e.g. the Doctor
        // Dekho homepage's WhatsApp CTA. Content never varies per call; callers are expected to
        // cache the response rather than re-fetch on every page view.
        [HttpGet("whatsapp-qr-code")]
        public async Task<IActionResult> GetWhatsAppEntryQrCode()
        {
            try
            {
                var response = await _mediator.Send(new GetWhatsAppEntryQrCodeRequestModel());
                if (!response.Success || response.Content == null)
                    return NotFound(new { response.Message });
                return File(response.Content, response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetWhatsAppEntryQrCode");
                return StatusCode(500, new { Message = "An error occurred while generating the QR code." });
            }
        }

        [HttpGet("doctors/{doctorId:guid}/availability")]
        public async Task<ActionResult<GetPublicDoctorAvailabilityResponseModel>> GetDoctorAvailability(Guid doctorId, [FromQuery] DateTime date)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorAvailabilityRequestModel
                {
                    DoctorId = doctorId,
                    Date = date,
                });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorAvailability for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching availability." });
            }
        }

        [HttpPost("appointments")]
        public async Task<ActionResult<PublicBookAppointmentResponseModel>> BookAppointment([FromBody] PublicBookAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Optional — a guest with no Authorization header (or an invalid/expired one) still
                // books fine, same as before. Only a genuinely valid patient session sets this,
                // which is what tells the CMS "Appointments" report guest vs. logged-in bookings apart.
                var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
                if (auth.IsValid) request.VerifiedMobile = auth.Mobile;

                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.BookAppointment");
                return StatusCode(500, new { Message = "An error occurred while booking the appointment." });
            }
        }

        // Anonymous cancel — the ONLY gate is knowing the AppointmentId (unguessable GUID), same
        // trust model as GetAppointment below, plus a Mobile cross-check in the handler. Built
        // for the WhatsApp bot (which only ever knows AppointmentId + the visitor's own phone
        // number, never a PatientId/HospitalId or a staff session) — see
        // PublicCancelAppointmentHandler for why this can't reuse the staff-JWT cancel endpoint.
        [HttpPatch("appointments/{appointmentId:guid}/cancel")]
        public async Task<ActionResult<PublicCancelAppointmentResponseModel>> CancelAppointment(
            Guid appointmentId, [FromBody] PublicCancelAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.AppointmentId = appointmentId;
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.CancelAppointment for appointmentId: {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while cancelling the appointment." });
            }
        }

        // Anonymous reschedule — same AppointmentId + Mobile gate as CancelAppointment above.
        [HttpPatch("appointments/{appointmentId:guid}/reschedule")]
        public async Task<ActionResult<PublicRescheduleAppointmentResponseModel>> RescheduleAppointment(
            Guid appointmentId, [FromBody] PublicRescheduleAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                request.AppointmentId = appointmentId;
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.RescheduleAppointment for appointmentId: {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while rescheduling the appointment." });
            }
        }

        // OPD QR check-in: converts a booked appointment into a queue token after a geofence check.
        // See IssueQueueTokenHandler for the idempotency/geofence details.
        [HttpPost("tokens")]
        public async Task<ActionResult<IssueQueueTokenResponseModel>> IssueQueueToken([FromBody] IssueQueueTokenRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.IssueQueueToken for appointmentId: {AppointmentId}", request.AppointmentId);
                return StatusCode(500, new { Message = "An error occurred while checking in." });
            }
        }

        // On-demand queue status snapshot -- deliberately a plain JSON GET, not SSE (no such
        // infrastructure exists in this codebase, and the bot's own POST /events/token-called
        // already handles real-time push delivery; this just serves a one-shot read for the bot's
        // "type STATUS" handler to call).
        [HttpGet("tokens/{appointmentId:guid}")]
        public async Task<ActionResult<GetQueueTokenStatusResponseModel>> GetQueueTokenStatus(Guid appointmentId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _mediator.Send(new GetQueueTokenStatusRequestModel { AppointmentId = appointmentId }, cancellationToken);
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetQueueTokenStatus for appointmentId: {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while fetching queue status." });
            }
        }

        // Walk-in OPD QR check-in: resolves "my appointment today at this hospital" from just a
        // phone number, for patients whose appointment wasn't booked through this bot (so the
        // bot doesn't already know an AppointmentId). See ResolveCheckInHandler for why this is
        // safe to expose anonymously (geofence-gated before any mobile lookup).
        [HttpPost("checkin/resolve")]
        public async Task<ActionResult<ResolveCheckInResponseModel>> ResolveCheckIn([FromBody] ResolveCheckInRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _mediator.Send(request, cancellationToken);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.ResolveCheckIn for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while checking in." });
            }
        }

        // Guest "my booking" lookup — gated purely by knowing the AppointmentId (unguessable GUID),
        // no login required. See GetPublicAppointmentHandler for why the response stays minimal.
        [HttpGet("appointments/{appointmentId:guid}")]
        public async Task<ActionResult<GetPublicAppointmentResponseModel>> GetAppointment(Guid appointmentId)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicAppointmentRequestModel { AppointmentId = appointmentId });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetAppointment for appointmentId: {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while fetching the appointment." });
            }
        }

        // Authenticated "my appointments" across every hospital — requires a patient JWT from
        // PatientAuthController's otp/verify. Validated manually here (never via [Authorize]/the
        // app's staff JWT-bearer pipeline) so a patient token can never be mistaken for a staff
        // session anywhere else in the app.
        [HttpGet("appointments/mine")]
        public async Task<ActionResult<GetPublicAppointmentsByMobileResponseModel>> GetMyAppointments(CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                var response = await _mediator.Send(new GetPublicAppointmentsByMobileRequestModel { Mobile = auth.Mobile });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetMyAppointments");
                return StatusCode(500, new { Message = "An error occurred while fetching your appointments." });
            }
        }

        // Documents (prescriptions/lab reports) attached to one of the caller's own appointments —
        // same manual-JWT validation as GetMyAppointments; the handler re-checks ownership via
        // PatientRegistration.Mobile so a guessed/adjacent AppointmentId can't leak someone else's
        // documents even with a valid patient session.
        [HttpGet("appointments/{appointmentId:guid}/documents")]
        public async Task<ActionResult<GetPublicAppointmentDocumentsResponseModel>> GetAppointmentDocuments(Guid appointmentId, CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                var response = await _mediator.Send(new GetPublicAppointmentDocumentsRequestModel { Mobile = auth.Mobile, AppointmentId = appointmentId });
                if (!response.Success && response.Message == "Appointment not found.") return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetAppointmentDocuments for appointmentId: {AppointmentId}", appointmentId);
                return StatusCode(500, new { Message = "An error occurred while fetching documents." });
            }
        }

        // Read-only "Personal Information" for the Doctor Dekho profile page — same manual-JWT
        // validation as GetMyAppointments, never [Authorize].
        [HttpGet("patients/me")]
        public async Task<ActionResult<GetPublicPatientProfileResponseModel>> GetMyPatientProfile(CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                var response = await _mediator.Send(new GetPublicPatientProfileRequestModel { Mobile = auth.Mobile });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetMyPatientProfile");
                return StatusCode(500, new { Message = "An error occurred while fetching your details." });
            }
        }

        // Health Locker — patient-initiated uploads independent of any appointment, keyed purely
        // by the OTP-verified Mobile (see PatientHealthLockerDocument). Same manual-JWT validation
        // as GetMyAppointments/GetMyPatientProfile.
        [HttpPost("patients/me/documents")]
        public async Task<ActionResult<UploadHealthLockerDocumentResponseModel>> UploadHealthLockerDocument(UploadHealthLockerDocumentRequestModel request, CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                request.Mobile = auth.Mobile;
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.UploadHealthLockerDocument");
                return StatusCode(500, new { Message = "An error occurred while uploading the document." });
            }
        }

        [HttpGet("patients/me/documents")]
        public async Task<ActionResult<GetHealthLockerDocumentsResponseModel>> GetHealthLockerDocuments(CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                var response = await _mediator.Send(new GetHealthLockerDocumentsRequestModel { Mobile = auth.Mobile });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetHealthLockerDocuments");
                return StatusCode(500, new { Message = "An error occurred while fetching your documents." });
            }
        }

        [HttpDelete("patients/me/documents/{documentId:guid}")]
        public async Task<ActionResult<DeleteHealthLockerDocumentResponseModel>> DeleteHealthLockerDocument(Guid documentId, CancellationToken cancellationToken)
        {
            var auth = await _patientTokenValidator.ValidateAsync(Request.Headers.Authorization.ToString(), cancellationToken);
            if (!auth.IsValid || auth.Mobile == null)
            {
                return Unauthorized(new { Message = auth.Reason ?? "Please log in again." });
            }

            try
            {
                var response = await _mediator.Send(new DeleteHealthLockerDocumentRequestModel { Mobile = auth.Mobile, DocumentId = documentId });
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.DeleteHealthLockerDocument for documentId: {DocumentId}", documentId);
                return StatusCode(500, new { Message = "An error occurred while deleting the document." });
            }
        }

        [HttpGet("doctors/{doctorId:guid}/reviews")]
        public async Task<ActionResult<GetPublicDoctorReviewsResponseModel>> GetDoctorReviews(Guid doctorId)
        {
            try
            {
                var response = await _mediator.Send(new GetPublicDoctorReviewsRequestModel { DoctorId = doctorId });
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.GetDoctorReviews for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while fetching reviews." });
            }
        }

        [HttpPost("doctors/{doctorId:guid}/reviews")]
        public async Task<ActionResult<SubmitDoctorReviewResponseModel>> SubmitDoctorReview(Guid doctorId, [FromBody] SubmitDoctorReviewRequestModel request)
        {
            try
            {
                request.DoctorId = doctorId;
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var response = await _mediator.Send(request);
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.SubmitDoctorReview for doctorId: {DoctorId}", doctorId);
                return StatusCode(500, new { Message = "An error occurred while submitting the review." });
            }
        }

        [HttpPatch("doctors/{doctorId:guid}/reviews/{reviewId:guid}")]
        public async Task<ActionResult<UpdateReviewCommentResponseModel>> UpdateReviewComment(Guid doctorId, Guid reviewId, [FromBody] UpdateReviewCommentRequestBody body)
        {
            try
            {
                var response = await _mediator.Send(new UpdateReviewCommentRequestModel { DoctorId = doctorId, ReviewId = reviewId, Comment = body.Comment });
                if (!response.Success) return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.UpdateReviewComment for reviewId: {ReviewId}", reviewId);
                return StatusCode(500, new { Message = "An error occurred while updating the review." });
            }
        }

        [HttpPost("doctors/{doctorId:guid}/reviews/{reviewId:guid}/helpful")]
        public async Task<ActionResult<MarkReviewHelpfulResponseModel>> MarkReviewHelpful(Guid doctorId, Guid reviewId)
        {
            try
            {
                var response = await _mediator.Send(new MarkReviewHelpfulRequestModel { ReviewId = reviewId });
                if (!response.Success) return NotFound(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublicController.MarkReviewHelpful for reviewId: {ReviewId}", reviewId);
                return StatusCode(500, new { Message = "An error occurred while marking the review helpful." });
            }
        }
    }
}
