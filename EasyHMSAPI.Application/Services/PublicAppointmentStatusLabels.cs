using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Patient-facing status wording for GET public/appointments/{id} and .../mine — the
    /// raw clinical workflow codes (VITALS_REQUIRED, UNDER_CONSULT, etc.) mean nothing to someone
    /// checking on their booking from outside the hospital, so anything that isn't
    /// pending/cancelled/completed is just "Confirmed" from a patient's point of view.</summary>
    public static class PublicAppointmentStatusLabels
    {
        public static string ToPatientLabel(string? statusCode)
        {
            if (statusCode == AppConstants.AppointmentStatus_PreAppointment) return "Pending Confirmation";
            if (statusCode == AppConstants.AppointmentStatus_Cancelled) return "Cancelled";
            if (statusCode == AppConstants.AppointmentStatus_Completed) return "Completed";
            if (string.IsNullOrEmpty(statusCode)) return "Pending Confirmation";
            return "Confirmed";
        }
    }
}
