using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Raw component inputs + computed per-component scores + TotalScore. SofaScoreCalculator
    /// (Application layer) is the single source of truth for how inputs map to each component's
    /// 0-4 score. Insert-only; typically re-scored daily to trend organ dysfunction.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("SofaScore")]
    public class SofaScore
    {
        [Key]
        public Guid SofaScoreId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public decimal? PaO2FiO2Ratio { get; set; }
        public bool OnRespiratorySupport { get; set; }
        public decimal? PlateletsCount { get; set; }
        public decimal? BilirubinMgDl { get; set; }
        public int? MapValue { get; set; }
        public string VasopressorTier { get; set; } = null!;
        public int? GcsTotal { get; set; }
        public decimal? CreatinineMgDl { get; set; }
        public decimal? UrineOutputMlPerDay { get; set; }

        public int RespiratoryScore { get; set; }
        public int CoagulationScore { get; set; }
        public int LiverScore { get; set; }
        public int CardiovascularScore { get; set; }
        public int CnsScore { get; set; }
        public int RenalScore { get; set; }
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
