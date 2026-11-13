using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A place Vita may show in an emergency (vision Phase 3, "Emergency takeover"): a 1HMS hospital that opted in from Settings
    // -> Emergency takeover (Source HOSPITAL), or a place a CMS admin added that is not on 1HMS (Source CMS, HospitalId null).
    // Listed to patients only while IsEnabled and VerificationStatus = VERIFIED. 1HMSDatabase create_tables_emergency_takeover.sql.
    [ExcludeFromCodeCoverage]
    [Table("HospitalEmergencySettings")]
    public class HospitalEmergencySetting
    {
        [Key]
        public Guid SettingId { get; set; }
        public Guid? HospitalId { get; set; }
        public string Source { get; set; } = "HOSPITAL";      // HOSPITAL | CMS
        public string? Name { get; set; }                      // CMS-added places only
        public string Kind { get; set; } = "HOSPITAL";        // HOSPITAL | AMBULANCE
        public bool IsEnabled { get; set; }
        public string EmergencyPhone { get; set; } = null!;
        public string? AlternatePhone { get; set; }
        public string? AlertWhatsApp { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Landmark { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool Is24x7 { get; set; } = true;
        public TimeSpan? OpenTime { get; set; }                // India time
        public TimeSpan? CloseTime { get; set; }
        public string? Capabilities { get; set; }              // comma list, see EmergencyRules.Capabilities
        public string? DutyContactName { get; set; }
        public string? DutyContactPhone { get; set; }
        public string? TermsVersion { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public Guid? TermsAcceptedByUserId { get; set; }
        public string VerificationStatus { get; set; } = "PENDING";   // PENDING | VERIFIED | REJECTED
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public string? VerificationNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Hospital? Hospital { get; set; }
    }
}
