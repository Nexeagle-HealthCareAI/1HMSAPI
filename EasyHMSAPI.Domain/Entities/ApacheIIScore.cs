using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Raw APS inputs + computed TotalScore. ApacheIIScoreCalculator (Application layer) is the
    /// single source of truth for how inputs map to points — this entity never re-derives them.
    /// Insert-only; conventionally scored once early in an ICU stay but re-scorable.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("ApacheIIScore")]
    public class ApacheIIScore
    {
        [Key]
        public Guid ApacheIIScoreId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public decimal? Temperature { get; set; }
        public int? MapValue { get; set; }
        public int? HeartRate { get; set; }
        public int? RespiratoryRate { get; set; }
        public decimal? FiO2 { get; set; }
        public decimal? PaO2 { get; set; }
        public decimal? ArterialPh { get; set; }
        public int? SerumSodium { get; set; }
        public decimal? SerumPotassium { get; set; }
        public decimal? SerumCreatinine { get; set; }
        public bool IsAcuteRenalFailure { get; set; }
        public decimal? Hematocrit { get; set; }
        public decimal? Wbc { get; set; }
        public int? GcsTotal { get; set; }

        public int? AgeYears { get; set; }
        public string ChronicHealthCategory { get; set; } = null!;

        public int TotalScore { get; set; }
        public string? Notes { get; set; }

        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
