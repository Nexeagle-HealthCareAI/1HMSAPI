using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Single place that mutates Admission.ReferralSource/ReferralName/ReferredByReferrerId AND
    /// writes the AdmissionReferrerAssignment span-row history, so every entry point (the dedicated
    /// ChangeAdmissionReferrer command and the generic UpdateAdmissionDetails edit form) produces the
    /// same audit trail -- mirrors AdmissionDoctorAssignmentHelper. Does not call SaveChangesAsync --
    /// the caller's own SaveChangesAsync flushes these tracked changes together with whatever else
    /// it's updating in the same request.
    /// </summary>
    public static class AdmissionReferrerAssignmentHelper
    {
        /// <summary>
        /// Releases the admission's current ACTIVE AdmissionReferrerAssignment row (if any) and
        /// inserts a new ACTIVE one, and updates the admission's live referral fields. No-ops
        /// (returns null) if nothing about the referrer actually changed, so a change isn't churned
        /// into a duplicate history row.
        /// </summary>
        public static async Task<AdmissionReferrerAssignment?> ChangeReferrerAsync(
            AppDbContext context, Admission admission, string referralSource, Guid? referrerId, string? referrerName, string? referrerType,
            string? changedBy, DateTime now, CancellationToken cancellationToken)
        {
            var normalizedSource = referralSource.Trim().ToUpperInvariant();
            var trimmedName = string.IsNullOrWhiteSpace(referrerName) ? null : referrerName.Trim();

            if (admission.ReferralSource == normalizedSource && admission.ReferredByReferrerId == referrerId && admission.ReferralName == trimmedName)
                return null;

            var current = await context.AdmissionReferrerAssignment
                .FirstOrDefaultAsync(a => a.AdmissionId == admission.AdmissionId && a.HospitalId == admission.HospitalId
                    && a.StatusCode == IpdConstants.ReferrerAssignmentStatus.Active, cancellationToken);

            if (current != null)
            {
                current.StatusCode = IpdConstants.ReferrerAssignmentStatus.Replaced;
                current.UnassignedAt = now;
                current.UnassignedBy = changedBy;
                current.UpdatedAt = now;
                current.UpdatedBy = changedBy;
            }

            var newAssignment = new AdmissionReferrerAssignment
            {
                AssignmentId = Guid.NewGuid(),
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                ReferralSource = normalizedSource,
                ReferrerId = referrerId,
                ReferrerName = trimmedName,
                ReferrerType = string.IsNullOrWhiteSpace(referrerType) ? null : referrerType.Trim().ToUpperInvariant(),
                AssignedAt = now,
                AssignedBy = changedBy,
                StatusCode = IpdConstants.ReferrerAssignmentStatus.Active,
                CreatedAt = now,
                CreatedBy = changedBy,
                UpdatedAt = now,
                UpdatedBy = changedBy,
            };
            context.AdmissionReferrerAssignment.Add(newAssignment);

            admission.ReferralSource = normalizedSource;
            admission.ReferredByReferrerId = referrerId;
            admission.ReferralName = trimmedName;
            return newAssignment;
        }
    }
}
