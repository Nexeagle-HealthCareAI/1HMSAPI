using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A SOAP-shaped doctor/nurse round note. Insert-only — edits older than the 24h
    /// lock window become addendum rows (IsAddendum/ParentNoteId), never overwrite history.</summary>
    [ExcludeFromCodeCoverage]
    [Table("RoundNote")]
    public class RoundNote
    {
        [Key]
        public Guid RoundNoteId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public Guid? DoctorId { get; set; }
        public string? DoctorName { get; set; }

        public DateTime NotedAt { get; set; }

        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? Diagnosis { get; set; }

        public bool IsAddendum { get; set; }
        public Guid? ParentNoteId { get; set; }
        public string? AddendumReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
