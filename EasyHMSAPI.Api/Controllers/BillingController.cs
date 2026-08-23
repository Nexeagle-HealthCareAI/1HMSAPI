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
    [Route("billing")]
    [Authorize]
    [RequiresPermission("billing")]
    public class BillingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BillingController> _logger;

        public BillingController(IMediator mediator, ILogger<BillingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("policy")]
        public async Task<ActionResult<GetBillingPolicyResponseModel>> GetBillingPolicy([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBillingPolicyRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingPolicy for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching billing policy." });
            }
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<GetHospitalBillingDashboardResponseModel>> GetBillingDashboard([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetHospitalBillingDashboardRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingDashboard for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching the billing dashboard." });
            }
        }

        [HttpGet("analytics/summary")]
        public async Task<ActionResult<GetBillingCategoryAnalyticsResponseModel>> GetBillingAnalyticsSummary(
            [FromQuery] Guid hospitalId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBillingCategoryAnalyticsRequestModel { HospitalId = hospitalId, StartDate = startDate, EndDate = endDate };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingAnalyticsSummary for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while fetching billing analytics." });
            }
        }

        [HttpGet("analytics/ai-insights")]
        public async Task<ActionResult<GetBillingAiInsightsResponseModel>> GetBillingAiInsights([FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });

            try
            {
                var request = new GetBillingAiInsightsRequestModel { HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingAiInsights for hospitalId: {HospitalId}", hospitalId);
                return StatusCode(500, new { Message = "An error occurred while generating AI insights." });
            }
        }

        [HttpGet("get-events")]
        public async Task<ActionResult<GetBillingEventsResponseModel>> GetBillingEvents(
            [FromQuery] Guid encounterId,
            [FromQuery] string? patientId,
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid? invoiceId)
        {
            if (hospitalId == Guid.Empty || encounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                var request = new GetBillingEventsRequestModel
                {
                    EncounterId = encounterId,
                    PatientId = patientId,
                    HospitalId = hospitalId,
                    InvoiceId = invoiceId,
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBillingEvents for encounterId: {EncounterId}", encounterId);
                return StatusCode(500, new { Message = "An error occurred while fetching billing events." });
            }
        }

        [HttpGet("get-event")]
        public async Task<ActionResult<GetPatientBillingEventsResponseModel>> GetPatientBillingEvents(
            [FromQuery] string? patientId,
            [FromQuery] Guid hospitalId)
        {
            if (hospitalId == Guid.Empty || string.IsNullOrWhiteSpace(patientId))
                return BadRequest(new { Message = "hospitalId and patientId are required." });

            try
            {
                var request = new GetPatientBillingEventsRequestModel { PatientId = patientId, HospitalId = hospitalId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPatientBillingEvents for patientId: {PatientId}", patientId);
                return StatusCode(500, new { Message = "An error occurred while fetching patient billing events." });
            }
        }

        [HttpDelete("delete-event")]
        public async Task<ActionResult<DeleteBillingEventResponseModel>> DeleteBillingEvent(
            [FromQuery] Guid hospitalId,
            [FromQuery] string? patientId,
            [FromQuery] Guid eventId,
            [FromQuery] string? type,
            [FromQuery] string? reason)
        {
            if (hospitalId == Guid.Empty || eventId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and eventId are required." });

            try
            {
                var request = new DeleteBillingEventRequestModel
                {
                    HospitalId = hospitalId,
                    PatientId = patientId,
                    EventId = eventId,
                    Type = type,
                    Reason = reason,
                    LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext),
                    LoggedInUserId = UserContextHelper.GetUserId(User),
                };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteBillingEvent for eventId: {EventId}", eventId);
                return StatusCode(500, new { Message = "An error occurred while deleting the billing event." });
            }
        }

        [HttpPost("invoice")]
        public async Task<ActionResult<CreateDraftInvoiceResponseModel>> CreateDraftInvoice([FromBody] CreateDraftInvoiceRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                request.LoggedInUserId = UserContextHelper.GetUserId(User);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateDraftInvoice for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while creating the invoice." });
            }
        }

        [HttpPost("finalize")]
        public async Task<ActionResult<FinalizeBillingResponseModel>> FinalizeBilling([FromQuery] string? type, [FromBody] FinalizeBillingRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                if (!string.IsNullOrWhiteSpace(type)) request.Type = type;
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FinalizeBilling for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while finalizing the bill." });
            }
        }

        // Manually deletes (soft-cancels) an invoice regardless of status -- draft or finalized.
        [HttpPost("delete-invoice")]
        public async Task<ActionResult<DeleteInvoiceResponseModel>> DeleteInvoice([FromBody] DeleteInvoiceRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty || request.InvoiceId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId, encounterId and invoiceId are required." });

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
                _logger.LogError(ex, "Error in DeleteInvoice for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while deleting the invoice." });
            }
        }

        [HttpGet("print")]
        public async Task<ActionResult<PrintBillingResponseModel>> PrintBilling(
            [FromQuery] string? patientId,
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid encounterId)
        {
            if (hospitalId == Guid.Empty || encounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                var request = new PrintBillingRequestModel { PatientId = patientId, HospitalId = hospitalId, EncounterId = encounterId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PrintBilling for encounterId: {EncounterId}", encounterId);
                return StatusCode(500, new { Message = "An error occurred while preparing the print." });
            }
        }

        [HttpPut("policy")]
        public async Task<ActionResult<UpsertBillingPolicyResponseModel>> UpdateBillingPolicy([FromBody] UpsertBillingPolicyRequestModel request)
        {
            if (request.HospitalId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId is required." });
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "Invalid request data", Errors = ModelState.Values.SelectMany(v => v.Errors) });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateBillingPolicy for hospitalId: {HospitalId}", request.HospitalId);
                return StatusCode(500, new { Message = "An error occurred while updating billing policy." });
            }
        }

        // ── Visit day-wise interim billing (opt-in, anchored to the visit; no admission) ─────
        [HttpGet("visit-day-bills")]
        public async Task<ActionResult<GetAdmissionDayBillsResponseModel>> GetVisitDayBills(
            [FromQuery] Guid hospitalId,
            [FromQuery] Guid encounterId)
        {
            if (hospitalId == Guid.Empty || encounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                var request = new GetAdmissionDayBillsRequestModel { HospitalId = hospitalId, EncounterId = encounterId };
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetVisitDayBills for encounterId: {EncounterId}", encounterId);
                return StatusCode(500, new { Message = "An error occurred while fetching day bills." });
            }
        }

        [HttpPost("visit-day/close")]
        public async Task<ActionResult<CloseAdmissionDayResponseModel>> CloseVisitDay([FromBody] CloseAdmissionDayRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and encounterId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CloseVisitDay for encounterId: {EncounterId}", request.EncounterId);
                return StatusCode(500, new { Message = "An error occurred while closing the day." });
            }
        }

        [HttpPost("visit-day/reopen")]
        public async Task<ActionResult<ReopenAdmissionDayResponseModel>> ReopenVisitDay([FromBody] ReopenAdmissionDayRequestModel request)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionDayBillId == Guid.Empty)
                return BadRequest(new { Message = "hospitalId and admissionDayBillId are required." });

            try
            {
                request.LoggedInUserName = await UserContextHelper.GetCurrentUserFullNameAsync(HttpContext);
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReopenAdmissionDay for billId: {BillId}", request.AdmissionDayBillId);
                return StatusCode(500, new { Message = "An error occurred while reopening the interim bill." });
            }
        }
    }
}
