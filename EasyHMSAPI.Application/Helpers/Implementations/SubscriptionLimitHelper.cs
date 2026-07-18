using EasyHMSAPI.Application.Helpers.Interfaces;
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

            var currentDoctorCount = await _context.Doctors
                .CountAsync(d => d.HospitalId == hospitalId, cancellationToken);

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
