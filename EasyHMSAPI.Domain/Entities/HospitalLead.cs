using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A hospital-scoped marketing lead -- someone searched for or viewed a doctor/hospital on
    // Doctor Dekho (NexEagleWebsite) or the WhatsApp bot. Deliberately a SEPARATE table from
    // AnalyticsEvents (see /public/track-event) rather than reusing it: AnalyticsEvents feeds
    // the platform-wide CMS Insights tab and has no HospitalId column at all; this table exists
    // purely to answer "what leads did hospital X get", with its own LeadType vocabulary (see
    // AppConstants.LeadType_*/LeadSource_*). No FK constraints on HospitalId/DoctorId, matching
    // AnalyticsEvents/WebsiteVisits' own convention for this class of lightweight event table.
    [ExcludeFromCodeCoverage]
    [Table("HospitalLeads")]
    public class HospitalLead
    {
        [Key]
        public Guid LeadId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? DoctorId { get; set; }

        public string Source { get; set; } = string.Empty;
        public string LeadType { get; set; } = string.Empty;

        // Raw typed text -- only set for search-type leads (DoctorNameSearch/HospitalNameSearch).
        public string? SearchQuery { get; set; }

        // Always known for WhatsApp; only known for web when the visitor is phone-verified.
        public string? Mobile { get; set; }
        public string? PatientName { get; set; }
        public string? SessionId { get; set; }

        // Web leads only -- resolved server-side the same way TrackEventHandler does.
        public string? IpAddress { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
