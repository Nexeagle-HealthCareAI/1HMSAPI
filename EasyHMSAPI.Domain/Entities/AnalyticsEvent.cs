using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Generic funnel/behavior event log fired by NexEagleWebsite (see /public/track-event) — see
    // AppConstants.AnalyticsEventType_* for the full set of EventType values in use.
    [ExcludeFromCodeCoverage]
    [Table("AnalyticsEvents")]
    public class AnalyticsEvent
    {
        [Key]
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public string? SessionId { get; set; }
        public string? Mobile { get; set; }
        public Guid? DoctorId { get; set; }
        // Frontend-only category slug (e.g. "cardiology") — not MedicalSpecialities.SpecialityId.
        public string? SpecialtyId { get; set; }

        public string? IpAddress { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }

        public string? MetadataJson { get; set; }
    }
}
