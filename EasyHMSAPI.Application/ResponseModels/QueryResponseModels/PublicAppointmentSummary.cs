using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // Deliberately minimal — this shape is returned by the ID-only guest lookup (GET
    // public/appointments/{id}), where the "auth" is just knowing the unguessable GUID. Anyone who
    // obtains that ID (screenshot, shared link, a lost device with it cached) can read this, so it
    // must never carry the patient's name, mobile, or reason-for-visit — only what's needed to
    // render a status card.
    [ExcludeFromCodeCoverage]
    public class PublicAppointmentSummary
    {
        public Guid AppointmentId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string HospitalName { get; set; } = string.Empty;
        public DateTime ApptDate { get; set; }
        public DateTime StartAt { get; set; }
        /// <summary>Human-readable ("Pending Confirmation", "Confirmed", "Completed", "Cancelled").</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Raw backend status code, in case a caller wants to branch on it directly.</summary>
        public string StatusCode { get; set; } = string.Empty;
        /// <summary>When someone at the hospital first saw or acted on the request (UTC); null if nobody has yet.</summary>
        public DateTime? HospitalSeenAt { get; set; }
        /// <summary>
        /// Who the visit is for. Filled ONLY in the signed-in patient's own list (GET public/appointments/mine), so a family
        /// with several bookings can tell them apart. Never set by the ID-only guest lookup (see the note at the top), and
        /// left out of the JSON when null.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? PatientName { get; set; }
    }
}
