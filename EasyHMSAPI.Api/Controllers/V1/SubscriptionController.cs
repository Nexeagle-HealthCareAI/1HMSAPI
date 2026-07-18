using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace EasyHMSAPI.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SubscriptionController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        // Server-to-server proxy to CMSAPI's plan catalog: the browser never talks to CMSAPI
        // directly (it has no CMS credential, and CMSAPI's own endpoints require CMS auth), and
        // CMSAPI's plans stay fully behind [Authorize] for everyone except this shared-key call.
        // [SkipHospitalAccessCheck] because a blocked/expired hospital must still be able to see
        // plans in order to pick one and pay.
        [HttpGet("plans")]
        [SkipHospitalAccessCheck]
        public async Task<IActionResult> GetPlans()
        {
            var baseUrl = _configuration["Cms:BaseUrl"];
            var serviceKey = _configuration["Cms:ServiceApiKey"];
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(serviceKey))
            {
                _logger.LogError("Cms:BaseUrl or Cms:ServiceApiKey is not configured; cannot fetch plans.");
                return StatusCode(503, new { Message = "The plan catalog is not available right now. Please try again later." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/v1/EasyHmsSubscriptionPlans/service");
                request.Headers.Add("X-Service-Key", serviceKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("CMS plan catalog request failed with {StatusCode}: {Body}", response.StatusCode, body);
                    return StatusCode(502, new { Message = "Could not load the plan catalog right now. Please try again later." });
                }

                return Content(body, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching plans from CMS.");
                return StatusCode(502, new { Message = "Could not load the plan catalog right now. Please try again later." });
            }
        }

        [HttpGet("{hospitalId}")]
        public async Task<IActionResult> GetSubscriptionStatus(Guid hospitalId)
        {
            var sub = await _context.HospitalSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId);

            if (sub == null)
            {
                return Ok(new { Status = "Trial", DaysLeft = 30 }); // Fallback
            }

            var daysLeft = 0;
            if (sub.Status == "Trial" && sub.TrialEndDate.HasValue)
            {
                daysLeft = (sub.TrialEndDate.Value - DateTime.UtcNow).Days;
                if (daysLeft < 0) daysLeft = 0;
            }
            else if (sub.Status == "Active" && sub.SubscriptionEndDate.HasValue)
            {
                daysLeft = (sub.SubscriptionEndDate.Value - DateTime.UtcNow).Days;
                if (daysLeft < 0) daysLeft = 0;
            }

            return Ok(new
            {
                sub.HospitalSubscriptionId,
                sub.PlanId,
                sub.Status,
                sub.TrialStartDate,
                sub.TrialEndDate,
                sub.SubscriptionStartDate,
                sub.SubscriptionEndDate,
                DaysLeft = daysLeft,
                sub.PaymentAmount,
                sub.PaymentReference,
                sub.PaymentDate
            });
        }

        [HttpPost("{hospitalId}/select-plan")]
        [SkipHospitalAccessCheck] // They might be blocked, so we must let them pay!
        public async Task<IActionResult> SelectPlan(Guid hospitalId, [FromBody] SelectPlanRequest request)
        {
            // Verify user is an admin for this hospital
            var userId = UserContextHelper.GetUserId(User);
            if (!userId.HasValue) return Unauthorized();

            var isAdmin = await _context.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserID == userId.Value 
                             && (ur.Role.HospitalID == null || ur.Role.HospitalID == hospitalId)
                             && (ur.Role.RoleName == "Admin" || ur.Role.RoleName == "AdminDoctor"));

            if (!isAdmin) return Forbid("Only administrators can manage subscriptions.");

            var sub = await _context.HospitalSubscriptions.FirstOrDefaultAsync(s => s.HospitalId == hospitalId);
            if (sub == null)
            {
                sub = new Domain.Entities.HospitalSubscription
                {
                    HospitalSubscriptionId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    Status = "Trial",
                    CreatedAt = DateTime.UtcNow,
                };
                _context.HospitalSubscriptions.Add(sub);
            }

            sub.PlanId = request.PlanId;
            sub.Status = "Pending"; // Waiting for CMS approval
            sub.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Plan selected and pending payment." });
        }

        [HttpPost("{hospitalId}/submit-payment")]
        [SkipHospitalAccessCheck]
        public async Task<IActionResult> SubmitPayment(Guid hospitalId, [FromBody] SubmitPaymentRequest request)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (!userId.HasValue) return Unauthorized();

            var isAdmin = await _context.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserID == userId.Value 
                             && (ur.Role.HospitalID == null || ur.Role.HospitalID == hospitalId)
                             && (ur.Role.RoleName == "Admin" || ur.Role.RoleName == "AdminDoctor"));

            if (!isAdmin) return Forbid("Only administrators can manage subscriptions.");

            var sub = await _context.HospitalSubscriptions.FirstOrDefaultAsync(s => s.HospitalId == hospitalId);
            if (sub == null) return NotFound("Subscription not found.");

            if (sub.Status != "Pending") 
            {
                // Might be updating an existing payment, or renewing
                if (sub.Status == "Active")
                {
                    // Allow renewing? The user wants to pay when it expires, before, or after.
                    // For now, let's allow it
                }
            }

            sub.PaymentAmount = request.Amount;
            sub.PaymentReference = request.Reference;
            sub.PaymentDate = DateTime.UtcNow;
            sub.Status = "PendingApproval";
            sub.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Payment submitted and pending approval." });
        }
    }

    public class SelectPlanRequest
    {
        public Guid PlanId { get; set; }
    }

    public class SubmitPaymentRequest
    {
        public decimal Amount { get; set; }
        public string Reference { get; set; }
    }
}
