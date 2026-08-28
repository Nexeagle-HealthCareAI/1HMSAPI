using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Single source of truth for the New / Old-Fee / Old-No-Fee decision.
    /// Used both when registering an appointment (RegisterAppointmentHandler, ConfirmPreAppointmentHandler)
    /// and when previewing the next visit's fee in the consult timeline, so the popup preview and the
    /// actual booking always agree. The window is anchored on the last fee appointment's date + the
    /// doctor's DoctorFee.FreeFollowUpDays (OPD_CONSULT). 0 = no free window at all, every visit is
    /// chargeable.
    /// </summary>
    public static class AppointmentTypeResolver
    {
        private const string OpdConsultFeeType = "OPD_CONSULT";

        public class Result
        {
            public string AppointmentType { get; set; } = AppConstants.AppointmentType_New;
            public DateTime? ValidUptoDate { get; set; }
            public bool FeeApplies =>
                !string.Equals(AppointmentType, AppConstants.AppointmentType_OldNoFee, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="hospitalId">Hospital the doctor's fee configuration belongs to.</param>
        /// <param name="requestPatientId">PatientId supplied on the request (may be null for a brand-new patient).</param>
        /// <param name="fallbackPatientId">Canonical PatientId to use when updating an existing appointment.</param>
        /// <param name="fullName">Patient full name, used to match an existing patient when no id is supplied.</param>
        /// <param name="targetDate">The date of the appointment being booked/previewed.</param>
        /// <param name="updatingAppointmentId">Non-null when an existing appointment is being updated — excluded
        /// from the "last appointment" lookup so an appointment never matches itself as its own history.</param>
        public static async Task<Result> ResolveAsync(
            AppDbContext context,
            Guid hospitalId,
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

            // Step 2: Doctor's free follow-up window (days). 0 = no free window, always chargeable.
            var freeFollowUpDays = await context.DoctorFees
                .Where(f => f.HospitalId == hospitalId && f.DoctorId == doctorId && f.FeeType == OpdConsultFeeType && f.IsActive)
                .Select(f => (int?)f.FreeFollowUpDays)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            var result = new Result();
            DateTime? effectiveDate;

            if (existingPatient is null)
            {
                // New patient.
                result.AppointmentType = AppConstants.AppointmentType_New;
                effectiveDate = CalculateFreeFollowUpUpto(targetDate, freeFollowUpDays);
            }
            else
            {
                // Existing patient - the last fee visit (New/Old-Fee, not cancelled/vitals-only), on or
                // before the target date, for this doctor. Excludes the appointment being updated so it
                // never matches itself, and excludes appointments after the target date so a backdated
                // or reordered visit is judged against what actually preceded it.
                var lastAppointment = await context.Appointments
                    .Where(a => a.PatientId == existingPatient.PatientId
                        && a.CurrentStatusCode != AppConstants.AppointmentStatus_VitalsRequired
                        && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                        && a.AppointmentType != AppConstants.AppointmentType_OldNoFee
                        && a.DoctorId == doctorId
                        && a.ApptDate <= targetDate
                        && (updatingAppointmentId == null || a.ApptId != updatingAppointmentId.Value))
                    .Select(x => new { x.ApptDate })
                    .OrderByDescending(a => a.ApptDate)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastAppointment is null)
                {
                    // No prior fee visit for this doctor - treat as new.
                    result.AppointmentType = AppConstants.AppointmentType_New;
                    effectiveDate = CalculateFreeFollowUpUpto(targetDate, freeFollowUpDays);
                }
                else
                {
                    var freeFollowUpExpiry = CalculateFreeFollowUpUpto(lastAppointment.ApptDate, freeFollowUpDays);

                    if (freeFollowUpExpiry is not null && targetDate <= freeFollowUpExpiry)
                    {
                        // Within the free-follow-up window - free follow-up.
                        result.AppointmentType = AppConstants.AppointmentType_OldNoFee;
                        effectiveDate = freeFollowUpExpiry;
                    }
                    else
                    {
                        // No free window, or outside it - chargeable follow-up.
                        result.AppointmentType = AppConstants.AppointmentType_OldFee;
                        effectiveDate = CalculateFreeFollowUpUpto(targetDate, freeFollowUpDays);
                    }
                }
            }

            result.ValidUptoDate = effectiveDate;
            return result;
        }

        /// <summary>
        /// The date up to which a follow-up booked on/after <paramref name="anchorDate"/> stays free,
        /// given a <paramref name="freeFollowUpDays"/> window. Null (no upto-date) when the window is
        /// zero or negative - i.e. there is no free window at all.
        /// </summary>
        public static DateTime? CalculateFreeFollowUpUpto(DateTime anchorDate, int freeFollowUpDays)
        {
            return freeFollowUpDays > 0 ? anchorDate.AddDays(freeFollowUpDays) : (DateTime?)null;
        }
    }
}
