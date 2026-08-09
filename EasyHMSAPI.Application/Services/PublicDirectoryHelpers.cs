using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Resolves which single hospital a doctor is publicly visible/bookable at — shared by
    /// GetPublicDoctorAvailabilityHandler and PublicBookAppointmentHandler so both apply the exact
    /// same "is this doctor+hospital pair genuinely public" rule GetPublicDoctorsHandler uses.
    ///
    /// A doctor is a global identity that can have DoctorDepartments rows at more than one hospital
    /// (Doctor.HospitalId is a single retrofitted field, not the source of truth — see
    /// GetDoctorFeesHandler). True multi-hospital doctor practice isn't a live product scenario yet,
    /// so when a doctor does have rows at more than one hospital, this picks one deterministically
    /// (the lowest HospitalId among their publicly-listed hospitals) rather than requiring a
    /// caller-supplied hospitalId — same one-hospital-per-doctor assumption GetPublicDoctorsHandler
    /// makes when building the directory listing.
    /// </summary>
    public static class PublicDirectoryHelpers
    {
        public static async Task<Guid?> ResolvePubliclyListedHospitalIdAsync(
            AppDbContext context, Guid doctorId, CancellationToken cancellationToken)
        {
            var doctor = await context.Doctors
                .Where(d => d.DoctorID == doctorId && d.IsPubliclyListed && !d.IsDelistedByAdmin)
                .Select(d => new { d.DoctorID, d.UserID })
                .FirstOrDefaultAsync(cancellationToken);
            if (doctor == null) return null;

            var userActive = await context.Users
                .AnyAsync(u => u.UserID == doctor.UserID && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
            if (!userActive) return null;

            var hospitalIds = await context.DoctorDepartments
                .Where(dd => dd.DoctorID == doctorId && dd.HospitalId.HasValue)
                .Select(dd => dd.HospitalId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (hospitalIds.Count == 0) return null;

            // This runs on every single availability check and every public booking — was one
            // query PER candidate hospital (rare in practice, but still a per-call round-trip
            // multiplier at volume). Batched into one query; .Min() picks the same "lowest
            // HospitalId among the publicly-listed ones" the old ordered-loop did.
            var publiclyListedIds = await context.Hospitals
                .Where(h => hospitalIds.Contains(h.HospitalID) && h.IsPubliclyListed && h.IsActive && !h.IsArchived)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            return publiclyListedIds.Count > 0 ? publiclyListedIds.Min() : (Guid?)null;
        }
    }
}
