using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Records the first moment hospital staff saw or acted on an appointment request (acknowledging the
    /// online-booking alert, opening the request, confirming, changing status, rescheduling or cancelling).
    /// Only the first time counts; later calls leave it unchanged.
    /// </summary>
    public static class AppointmentSeen
    {
        public const string OnlineRequestAlertCode = "ONLINE_APPOINTMENT_REQUEST";

        /// <returns>True when this call set it (it was not seen before).</returns>
        public static bool Mark(Appointment appointment, Guid? userId, DateTime? nowUtc = null)
        {
            if (appointment.HospitalSeenAt.HasValue) return false;
            appointment.HospitalSeenAt = nowUtc ?? DateTime.UtcNow;
            appointment.HospitalSeenByUserId = userId is { } id && id != Guid.Empty ? id : null;
            return true;
        }
    }
}
