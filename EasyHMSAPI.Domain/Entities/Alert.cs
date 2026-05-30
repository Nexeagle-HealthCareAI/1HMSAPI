using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("Alert")]
    public class Alert
    {
        [Key]
        public Guid AlertId { get; set; }
        public Guid HospitalId { get; set; }

        public string AlertCode { get; set; } = null!;
        public string Severity { get; set; } = "INFO";   // INFO / WARNING / CRITICAL
        public string Title { get; set; } = null!;
        public string? Body { get; set; }

        public string? PatientId { get; set; }
        public Guid? AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }

        public string? AudienceRoles { get; set; }        // comma-joined role list
        public Guid? AudienceUserId { get; set; }
        public string? AudienceWardCode { get; set; }

        public string Status { get; set; } = "ACTIVE";    // ACTIVE / ACKNOWLEDGED / DISMISSED / SNOOZED / EXPIRED

        public DateTime RaisedAt { get; set; }
        public string? RaisedBy { get; set; }
        public Guid? RaisedByUserId { get; set; }
        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        public bool DispatchSms { get; set; }
        public bool DispatchWhatsApp { get; set; }
        public bool DispatchInApp { get; set; } = true;
        public string? DispatchToPhone { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string? DispatchError { get; set; }

        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
        public Guid? AcknowledgedByUserId { get; set; }
        public string? AcknowledgeNote { get; set; }

        public DateTime? DismissedAt { get; set; }
        public string? DismissedBy { get; set; }
        public Guid? DismissedByUserId { get; set; }
        public string? DismissReason { get; set; }

        public DateTime? SnoozedUntil { get; set; }
        public string? PayloadJson { get; set; }

        public DateTime CreatedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
