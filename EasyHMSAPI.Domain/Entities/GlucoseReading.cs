using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One glucose reading, with optional insulin-given tracking. ValueMgDl/IsHypo/
    /// IsHyper are app-computed at write time — no DB enforcement (deliberately loose).</summary>
    [ExcludeFromCodeCoverage]
    [Table("GlucoseReading")]
    public class GlucoseReading
    {
        [Key]
        public Guid GlucoseReadingId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public decimal Value { get; set; }
        public string Unit { get; set; } = "mg/dL";
        public decimal ValueMgDl { get; set; }

        public string? Method { get; set; }
        public string? MealTag { get; set; }

        public bool InsulinGiven { get; set; }
        public decimal? InsulinUnits { get; set; }
        public string? InsulinType { get; set; }
        public string? InsulinRoute { get; set; }

        public bool IsHypo { get; set; }
        public bool IsHyper { get; set; }

        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public Guid? RecordedByUserId { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
