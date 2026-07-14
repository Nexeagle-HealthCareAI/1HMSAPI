using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// NEWS2-style composite deterioration score. Raw component inputs + computed per-component
    /// scores + TotalScore, same shape as SofaScore/ApacheIIScore. EarlyWarningScoreCalculator
    /// (Application layer) is the single source of truth for how inputs map to each component's
    /// 0-3 score. Insert-only. Applies to any IPD admission, not just ICU — a deteriorating ward
    /// patient should be flagged before a crisis, not only tracked once they reach ICU.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("EarlyWarningScore")]
    public class EarlyWarningScore
    {
        [Key]
        public Guid ScoreId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public int? RespiratoryRate { get; set; }
        public decimal? Spo2 { get; set; }
        public bool SupplementalOxygen { get; set; }
        public int? SystolicBp { get; set; }
        public int? Pulse { get; set; }
        public string ConsciousnessLevel { get; set; } = "ALERT";
        public decimal? TemperatureC { get; set; }

        public int RrScore { get; set; }
        public int Spo2Score { get; set; }
        public int O2Score { get; set; }
        public int BpScore { get; set; }
        public int PulseScore { get; set; }
        public int ConsciousnessScore { get; set; }
        public int TempScore { get; set; }
        public int TotalScore { get; set; }
        public string RiskBand { get; set; } = null!;

        public string? Notes { get; set; }

        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
