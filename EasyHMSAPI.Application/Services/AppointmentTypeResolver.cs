using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Single source of truth for the New / Old-Fee / Old-No-Fee decision.
    /// Used both when registering an appointment (RegisterAppointmentHandler) and when previewing
    /// the next visit's fee in the consult timeline, so the popup preview and the actual booking
    /// always agree. The window is anchored on the last fee appointment's date + the doctor's
    /// PrescriptionSetting.ValidDuration (0 = never expires).
    /// </summary>
    public static class AppointmentTypeResolver
    {
        public class Result
        {
            public string AppointmentType { get; set; } = AppConstants.AppointmentType_New;
            public DateTime? ValidUptoDate { get; set; }
            public bool FeeApplies =>
                !string.Equals(AppointmentType, AppConstants.AppointmentType_OldNoFee, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="requestPatientId">PatientId supplied on the request (may be null for a brand-new patient).</param>
        /// <param name="fallbackPatientId">Canonical PatientId to use when updating an existing appointment.</param>
        /// <param name="fullName">Patient full name, used to match an existing patient when no id is supplied.</param>
        /// <param name="targetDate">The date of the appointment being booked/previewed.</param>
        /// <param name="updatingAppointmentId">Non-null when an existing appointment is being updated.</param>
        public static async Task<Result> ResolveAsync(
            AppDbContext context,
            string? requestPatientId,
            string? fallbackPatientId,
            string? fullName,
            Guid doctorId,
            DateTime targetDate,
            Guid? updatingAppointmentId,
            CancellationToken cancellationToken)
        {
            // Step 1: Find existing patient by ID or name.
            var patientIdToSearch = (requestPatientId is null && updatingAppointmentId is not null)
                ? fallbackPatientId ?? string.Empty
                : requestPatientId?.ToUpper() ?? string.Empty;

            var existingPatient = await context.PatientRegistrations
                .Where(p => p.PatientId != null && p.PatientId.ToUpper() == patientIdToSearch)
                .Select(x => new { x.PatientId, x.FullName })
                .FirstOrDefaultAsync(cancellationToken);

            if (existingPatient is null && !string.IsNullOrWhiteSpace(fullName))
            {
                var requestFullName = fullName.Trim().ToLower();
                existingPatient = await context.PatientRegistrations
                    .Where(p => p.FullName != null && p.FullName.Trim().ToLower() == requestFullName)
                    .Select(x => new { x.PatientId, x.FullName })
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Step 2: Doctor's prescription validity window (days). 0 = never expires.
            var validDuration = await context.PrescriptionSettings
                .Where(ps => ps.DoctorId == doctorId)
                .Select(ps => (int?)ps.ValidDuration)
                .FirstOrDefaultAsync(cancellationToken);

            var result = new Result();
            DateTime? effectiveDate;

            if (existingPatient is null)
            {
                // New patient.
                result.AppointmentType = AppConstants.AppointmentType_New;
                effectiveDate = CalculateValidUptoDate(validDuration, targetDate);
            }
            else
            {
                // Existing patient - the last fee visit (New/Old-Fee, not cancelled/vitals-only) for this doctor.
                var lastAppointment = await context.Appointments
                    .Where(a => a.PatientId == existingPatient.PatientId
                        && a.CurrentStatusCode != AppConstants.AppointmentStatus_VitalsRequired
                        && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                        && a.AppointmentType != AppConstants.AppointmentType_OldNoFee
                        && a.DoctorId == doctorId)
                    .Select(x => new { x.ApptDate, x.ApptId, x.DoctorId, x.AppointmentType })
                    .OrderByDescending(a => a.ApptDate)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastAppointment is null)
                {
                    // No prior visit for this doctor - treat as new.
                    result.AppointmentType = AppConstants.AppointmentType_New;
                    effectiveDate = CalculateValidUptoDate(validDuration, targetDate);
                }
                else if (updatingAppointmentId is not null && lastAppointment.AppointmentType == AppConstants.AppointmentType_New)
                {
                    // Updating an appointment whose last fee visit was itself New.
                    result.AppointmentType = AppConstants.AppointmentType_New;
                    effectiveDate = CalculateValidUptoDate(validDuration, targetDate);
                }
                else
                {
                    var lastPrescriptionExpiry = GetPrescriptionExpiry(lastAppointment.ApptDate, validDuration);

                    if (lastPrescriptionExpiry is null || targetDate <= lastPrescriptionExpiry)
                    {
                        // Within validity (or never expires) - free follow-up.
                        result.AppointmentType = AppConstants.AppointmentType_OldNoFee;
                        effectiveDate = lastPrescriptionExpiry;
                    }
                    else
                    {
                        // Outside validity - chargeable follow-up.
                        result.AppointmentType = AppConstants.AppointmentType_OldFee;
                        effectiveDate = CalculateValidUptoDate(validDuration, targetDate);
                    }
                }
            }

            result.ValidUptoDate = effectiveDate;
            return result;
        }

        public static DateTime? GetPrescriptionExpiry(DateTime lastApptDate, int? validDuration)
        {
            if (validDuration is null) return null;
            return validDuration == 0 ? (DateTime?)null : lastApptDate.AddDays(validDuration.Value);
        }

        public static DateTime? CalculateValidUptoDate(int? validDuration, DateTime appointmentDate)
        {
            if (validDuration is null) return null;
            return validDuration == 0 ? (DateTime?)null : appointmentDate.AddDays(validDuration.Value);
        }
    }
}
