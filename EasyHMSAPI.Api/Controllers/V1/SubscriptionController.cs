using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubscriptionController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{hospitalId}")]
        public async Task<IActionResult> GetSubscriptionStatus(Guid hospitalId)
        {
            var sub = await _context.HospitalSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId);

            if (sub == null)
            {
                return Ok(new { Status = "Trial", DaysLeft = 14 }); // Fallback
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
