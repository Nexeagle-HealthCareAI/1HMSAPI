using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Single place that mutates Admission.PrimaryDoctorId AND writes the AdmissionDoctorAssignment
    /// span-row history, so every entry point (the dedicated ChangeAdmittingDoctor command and the
    /// generic UpdateAdmissionDetails edit form) produces the same audit trail. Does not call
    /// SaveChangesAsync -- the caller's own SaveChangesAsync flushes these tracked changes together
    /// with whatever else it's updating in the same request.
    /// </summary>
    public static class AdmissionDoctorAssignmentHelper
    {
        /// <summary>
        /// Releases the admission's current ACTIVE AdmissionDoctorAssignment row (if any) and inserts
        /// a new ACTIVE one for newDoctorId, and updates admission.PrimaryDoctorId. No-ops (returns
        /// null) if newDoctorId already matches the admission's current PrimaryDoctorId, so a change
        /// isn't churned into a duplicate history row.
        /// </summary>
        public static async Task<AdmissionDoctorAssignment?> ChangeDoctorAsync(
            AppDbContext context, Admission admission, Guid newDoctorId,
            string? changedBy, DateTime now, CancellationToken cancellationToken)
        {
            if (admission.PrimaryDoctorId == newDoctorId) return null;

            var current = await context.AdmissionDoctorAssignment
                .FirstOrDefaultAsync(a => a.AdmissionId == admission.AdmissionId && a.HospitalId == admission.HospitalId
                    && a.StatusCode == IpdConstants.DoctorAssignmentStatus.Active, cancellationToken);

            if (current != null)
            {
                current.StatusCode = IpdConstants.DoctorAssignmentStatus.Replaced;
                current.UnassignedAt = now;
                current.UnassignedBy = changedBy;
                current.UpdatedAt = now;
                current.UpdatedBy = changedBy;
            }

            var newAssignment = new AdmissionDoctorAssignment
            {
                AssignmentId = Guid.NewGuid(),
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                DoctorId = newDoctorId,
                AssignedAt = now,
                AssignedBy = changedBy,
                StatusCode = IpdConstants.DoctorAssignmentStatus.Active,
                CreatedAt = now,
                CreatedBy = changedBy,
                UpdatedAt = now,
                UpdatedBy = changedBy,
            };
            context.AdmissionDoctorAssignment.Add(newAssignment);

            admission.PrimaryDoctorId = newDoctorId;
            return newAssignment;
        }
    }
}
