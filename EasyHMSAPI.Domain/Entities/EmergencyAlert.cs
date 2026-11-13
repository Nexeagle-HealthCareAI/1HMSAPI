using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Audit of the emergency takeover: every alert sent to a place, every call button a patient tapped and every number a patient chose
    // to share, grouped by EventId (one patient emergency). Distance only, never the patient's point; PatientMobile only for NUMBER_SHARED.
    [ExcludeFromCodeCoverage]
    [Table("EmergencyAlerts")]
    public class EmergencyAlert
    {
        [Key]
        public Guid EmergencyAlertId { get; set; }
        public Guid EventId { get; set; }
        public Guid SettingId { get; set; }
        public Guid? HospitalId { get; set; }
        public string Kind { get; set; } = "ALERT";        // ALERT | CALL_TAP | NUMBER_SHARED
        public string? Reason { get; set; }
        public decimal? DistanceKm { get; set; }
        public string? Channel { get; set; }               // web | whatsapp
        public string? SessionRef { get; set; }
        public string? PatientMobile { get; set; }
        public bool WhatsAppSent { get; set; }
        public string? WhatsAppMessageId { get; set; }
        public Guid? InAppAlertId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
