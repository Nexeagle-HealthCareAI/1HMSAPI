using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One nursing-assessment snapshot: Morse Fall Scale + Braden Pressure-Ulcer Scale +
    /// MUST nutrition screen. Insert-only — re-assess by inserting a new row, trend over time.</summary>
    [ExcludeFromCodeCoverage]
    [Table("NursingAssessment")]
    public class NursingAssessment
    {
        [Key]
        public Guid NursingAssessmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public DateTime AssessedAt { get; set; }
        public string? AssessedBy { get; set; }
        public Guid? AssessedByUserId { get; set; }

        public int MorseHistoryOfFalling { get; set; }
        public int MorseSecondaryDiagnosis { get; set; }
        public int MorseAmbulatoryAid { get; set; }
        public int MorseIvHeparinLock { get; set; }
        public int MorseGait { get; set; }
        public int MorseMentalStatus { get; set; }
        public int MorseTotal { get; set; }
        public string MorseRisk { get; set; } = "NONE";

        public int BradenSensoryPerception { get; set; } = 4;
        public int BradenMoisture { get; set; } = 4;
        public int BradenActivity { get; set; } = 4;
        public int BradenMobility { get; set; } = 4;
        public int BradenNutrition { get; set; } = 4;
        public int BradenFrictionShear { get; set; } = 3;
        public int BradenTotal { get; set; } = 23;
        public string BradenRisk { get; set; } = "NONE";

        public int MustBmiScore { get; set; }
        public int MustWeightLossScore { get; set; }
        public int MustAcuteDiseaseScore { get; set; }
        public int MustTotal { get; set; }
        public string MustRisk { get; set; } = "LOW";

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
