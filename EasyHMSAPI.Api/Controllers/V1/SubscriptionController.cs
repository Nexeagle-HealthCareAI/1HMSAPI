using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.Helpers.Interfaces;
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
        private readonly ISubscriptionLimitHelper _subscriptionLimitHelper;

        public SubscriptionController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SubscriptionController> logger, ISubscriptionLimitHelper subscriptionLimitHelper)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _subscriptionLimitHelper = subscriptionLimitHelper;
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

        // [SkipHospitalAccessCheck] because a hospital whose trial/subscription has expired must
        // still be able to see its own status (that's how the admin knows to renew) — this endpoint
        // is read-only about the hospital's own subscription, not a tenant-scoped feature.
        [HttpGet("{hospitalId}")]
        [SkipHospitalAccessCheck]
        public async Task<IActionResult> GetSubscriptionStatus(Guid hospitalId)
        {
            var sub = await _context.HospitalSubscriptions
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId);

            if (sub == null)
            {
                return Ok(new { Status = "Trial", DaysLeft = 30 }); // Fallback
            }

            var effectiveStatus = sub.GetEffectiveStatus(DateTime.UtcNow);
            if (effectiveStatus != sub.Status)
            {
                // Persist the transition so other consumers (e.g. the CMS approval dashboard) see
                // an accurate status instead of a stale "Trial"/"Active" row.
                sub.Status = effectiveStatus;
                sub.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            var daysLeft = 0;
            if (effectiveStatus == "Trial" && sub.TrialEndDate.HasValue)
            {
                daysLeft = (sub.TrialEndDate.Value - DateTime.UtcNow).Days;
                if (daysLeft < 0) daysLeft = 0;
            }
            else if (effectiveStatus == "Active" && sub.SubscriptionEndDate.HasValue)
            {
                daysLeft = (sub.SubscriptionEndDate.Value - DateTime.UtcNow).Days;
                if (daysLeft < 0) daysLeft = 0;
            }

            return Ok(new
            {
                sub.HospitalSubscriptionId,
                sub.PlanId,
                Status = effectiveStatus,
                sub.TrialStartDate,
                sub.TrialEndDate,
                sub.SubscriptionStartDate,
                sub.SubscriptionEndDate,
                DaysLeft = daysLeft,
                sub.PaymentAmount,
                sub.PaymentReference,
                sub.PaymentMode,
                sub.PaymentDate,
                sub.RejectionReason,
                sub.RejectedAt,
                sub.ReferralCode,
                sub.ReferralCodeRewardKind,
                sub.ReferralCodeRewardValue,
                sub.ReferralCodeRedeemedAt
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
            sub.PaymentMode = request.PaymentMode;
            sub.PaymentDate = DateTime.UtcNow;
            sub.Status = "PendingApproval";
            // Clear any previous rejection now that a fresh payment has been submitted for review.
            sub.RejectionReason = null;
            sub.RejectedAt = null;
            sub.UpdatedAt = DateTime.UtcNow;

            // A hospital submitting a new switch while an earlier submission is still awaiting
            // review (e.g. they changed their mind about which plan to switch to) shouldn't leave
            // two conflicting PendingApproval rows for CMS to see — supersede the old one(s).
            var stillPending = await _context.HospitalSubscriptionPayments
                .Where(p => p.HospitalId == hospitalId && p.Status == "PendingApproval")
                .ToListAsync();
            foreach (var stale in stillPending)
            {
                stale.Status = "Superseded";
                stale.ReviewedAt = DateTime.UtcNow;
            }

            _context.HospitalSubscriptionPayments.Add(new Domain.Entities.HospitalSubscriptionPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = hospitalId,
                HospitalSubscriptionId = sub.HospitalSubscriptionId,
                PlanId = sub.PlanId,
                Amount = request.Amount,
                Reference = request.Reference,
                PaymentMode = request.PaymentMode,
                Status = "PendingApproval",
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsProratedSwitch = request.IsProratedSwitch,
                PreviousPlanId = request.PreviousPlanId,
                PreviousPlanName = request.PreviousPlanName,
                ProratedCreditAmount = request.ProratedCreditAmount
            });

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Payment submitted and pending approval." });
        }

        // [SkipHospitalAccessCheck] for the same reason as GetSubscriptionStatus — a hospital that's
        // currently locked out still needs to see its own payment history (e.g. to confirm a
        // rejected payment's details before resubmitting).
        [HttpGet("{hospitalId}/payment-history")]
        [SkipHospitalAccessCheck]
        public async Task<IActionResult> GetPaymentHistory(Guid hospitalId)
        {
            var history = await _context.HospitalSubscriptionPayments
                .AsNoTracking()
                .Where(p => p.HospitalId == hospitalId)
                .OrderByDescending(p => p.SubmittedAt)
                .Select(p => new
                {
                    p.PaymentId,
                    p.PlanId,
                    p.PlanName,
                    p.Amount,
                    p.Reference,
                    p.PaymentMode,
                    p.Status,
                    p.SubmittedAt,
                    p.ReviewedAt,
                    p.RejectionReason,
                    p.IsProratedSwitch,
                    p.PreviousPlanName,
                    p.ProratedCreditAmount
                })
                .ToListAsync();

            return Ok(history);
        }

        // Lets the frontend show "X of Y doctors/beds used" banners in Bed Management and User
        // Management without duplicating the counting rules that live in SubscriptionLimitHelper
        // (e.g. revoked users don't count against the doctor limit). Null Max* means unlimited.
        [HttpGet("{hospitalId}/usage")]
        [SkipHospitalAccessCheck]
        public async Task<IActionResult> GetUsage(Guid hospitalId)
        {
            var usage = await _subscriptionLimitHelper.GetUsageAsync(hospitalId, HttpContext.RequestAborted);

            return Ok(new
            {
                usage.MaxDoctors,
                usage.CurrentDoctors,
                usage.MaxBeds,
                usage.CurrentBeds
            });
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
        public string? PaymentMode { get; set; }

        // Present when this submission is a mid-cycle plan switch (upgrade/downgrade) from an
        // already-Active subscription — Amount above already has the credit applied; these are
        // carried through purely so CMS can see/verify the breakdown before approving.
        public bool IsProratedSwitch { get; set; }
        public Guid? PreviousPlanId { get; set; }
        public string? PreviousPlanName { get; set; }
        public decimal? ProratedCreditAmount { get; set; }
    }
}
