using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A nurse-to-nurse shift handover — structured SBAR (Situation/Background/
    /// Assessment/Recommendation) with a free-text fallback so a nurse is never forced into
    /// the structured fields (only Situation is ever mandatory, and IsFreeText bypasses SBAR
    /// entirely). Separate concept from RoundNote (doctor round documentation).</summary>
    [ExcludeFromCodeCoverage]
    [Table("ShiftHandoverNote")]
    public class ShiftHandoverNote
    {
        [Key]
        public Guid ShiftHandoverNoteId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string ShiftCode { get; set; } = null!;   // MORNING / EVENING / NIGHT
        public DateTime ShiftDate { get; set; }           // IST calendar date, date-only

        public string OutgoingNurseName { get; set; } = null!;
        public Guid? OutgoingNurseUserId { get; set; }
        public string? IncomingNurseName { get; set; }
        public Guid? IncomingNurseUserId { get; set; }
        public DateTime? IncomingAckAt { get; set; }

        public bool IsFreeText { get; set; }
        public string? FreeTextNote { get; set; }

        public string? Situation { get; set; }
        public string? Background { get; set; }
        public string? Assessment { get; set; }
        public string? Recommendation { get; set; }

        public DateTime HandoverAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
