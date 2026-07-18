using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Helpers.Implementations
{
    public class SubscriptionLimitHelper : ISubscriptionLimitHelper
    {
        private readonly AppDbContext _context;

        public SubscriptionLimitHelper(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubscriptionLimitResult> CanAddDoctorAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var sub = await _context.HospitalSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId, cancellationToken);

            if (sub?.MaxDoctors == null) return new SubscriptionLimitResult(true, null);

            // Doctor has no active/inactive flag of its own — a departed team member's row
            // persists forever, so count only doctors whose linked User account isn't revoked
            // (mirrors the check DoctorCreateHandler/DeactivateUserHandler already use), so
            // deactivating someone actually frees their slot against the plan's doctor limit.
            var currentDoctorCount = await _context.Doctors
                .CountAsync(d => d.HospitalId == hospitalId && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);

            if (currentDoctorCount >= sub.MaxDoctors.Value)
            {
                return new SubscriptionLimitResult(false,
                    $"Your current subscription plan allows up to {sub.MaxDoctors.Value} doctor(s). Upgrade your plan to add more.");
            }

            return new SubscriptionLimitResult(true, null);
        }

        public async Task<SubscriptionLimitResult> CanAddBedsAsync(Guid hospitalId, int additionalBeds, CancellationToken cancellationToken)
        {
            var sub = await _context.HospitalSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId, cancellationToken);

            if (sub?.MaxBeds == null) return new SubscriptionLimitResult(true, null);

            var currentBedCount = await _context.BedMaster
                .CountAsync(b => b.HospitalId == hospitalId && b.IsActive, cancellationToken);

            if (currentBedCount + additionalBeds > sub.MaxBeds.Value)
            {
                return new SubscriptionLimitResult(false,
                    $"Your current subscription plan allows up to {sub.MaxBeds.Value} bed(s). You currently have {currentBedCount}. Upgrade your plan to add more.");
            }

            return new SubscriptionLimitResult(true, null);
        }
    }
}
