using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// One place for the rules about putting a patient in a bed and keeping BedMaster.StatusCode truthful.
    /// The bed board reads StatusCode directly, so assigning must make the bed OCCUPIED and leaving it must
    /// make it CLEANING; before this, nothing ever did and the status drifted from the real assignments.
    /// </summary>
    public static class BedOccupancy
    {
        /// <summary>Null when the bed can take this patient now, otherwise the message to show.</summary>
        public static async Task<string?> CheckAssignableAsync(AppDbContext context, BedMaster bed, string? patientSex, CancellationToken cancellationToken)
        {
            var label = string.IsNullOrWhiteSpace(bed.BedCode) ? "That bed" : $"Bed {bed.BedCode}";

            if (!bed.IsActive)
                return $"{label} is inactive and cannot be assigned.";

            // A live assignment is the truth, whatever the status column says.
            var occupied = await context.BedAssignment.AnyAsync(a => a.BedId == bed.BedId && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
            if (occupied)
                return $"{label} is already occupied by another patient.";

            var status = string.IsNullOrWhiteSpace(bed.StatusCode) ? IpdConstants.BedStatus.Available : bed.StatusCode.Trim().ToUpperInvariant();
            if (status != IpdConstants.BedStatus.Available)
            {
                return status switch
                {
                    IpdConstants.BedStatus.Occupied => $"{label} is marked occupied.",
                    IpdConstants.BedStatus.Cleaning => $"{label} is being cleaned. Mark it available once it is ready.",
                    IpdConstants.BedStatus.Reserved => $"{label} is reserved and cannot be assigned.",
                    IpdConstants.BedStatus.Blocked => $"{label} is blocked and cannot be assigned.",
                    _ => $"{label} is not available (status {status}).",
                };
            }

            var restriction = bed.GenderRestriction?.Trim().ToUpperInvariant();
            if (restriction == "MALE_ONLY" || restriction == "FEMALE_ONLY")
            {
                var sex = NormaliseSex(patientSex);
                if (sex == null)
                    return $"{label} is restricted to {(restriction == "MALE_ONLY" ? "male" : "female")} patients, and the patient's sex is not recorded. Record it first.";
                if ((restriction == "MALE_ONLY") != (sex == "M"))
                    return $"{label} is restricted to {(restriction == "MALE_ONLY" ? "male" : "female")} patients.";
            }

            return null;
        }

        public static async Task<string?> LoadPatientSexAsync(AppDbContext context, Guid hospitalId, string? patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return null;
            return await context.PatientRegistrations
                .Where(p => p.PatientId == patientId && p.HospitalId == hospitalId)
                .Select(p => p.Sex)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>"M" / "F", or null when the value is missing or not a definite male/female.</summary>
        public static string? NormaliseSex(string? raw)
        {
            var s = raw?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return null;
            if (s is "M" or "MALE") return "M";
            if (s is "F" or "FEMALE") return "F";
            return null;
        }

        public static void MarkOccupied(BedMaster bed, DateTime now, string? by) => SetStatus(bed, IpdConstants.BedStatus.Occupied, now, by);

        /// <summary>The patient left: the bed needs cleaning before anyone else can be put in it.</summary>
        public static void MarkVacated(BedMaster bed, DateTime now, string? by) => SetStatus(bed, IpdConstants.BedStatus.Cleaning, now, by);

        private static void SetStatus(BedMaster bed, string status, DateTime now, string? by)
        {
            bed.StatusCode = status;
            bed.LastStatusAt = now;
            bed.UpdatedAt = now;
            bed.UpdatedBy = by;
        }

        /// <summary>True when a patient is in the bed right now (an ACTIVE assignment), regardless of StatusCode.</summary>
        public static Task<bool> HasActiveAssignmentAsync(AppDbContext context, Guid bedId, CancellationToken cancellationToken)
            => context.BedAssignment.AnyAsync(a => a.BedId == bedId && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
    }
}
