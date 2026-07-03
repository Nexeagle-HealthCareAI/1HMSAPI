using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetNursingAssessmentsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<NursingAssessmentItem> Assessments { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class NursingAssessmentItem
    {
        public Guid NursingAssessmentId { get; set; }
        public DateTime AssessedAt { get; set; }
        public string? AssessedBy { get; set; }

        public int MorseHistoryOfFalling { get; set; }
        public int MorseSecondaryDiagnosis { get; set; }
        public int MorseAmbulatoryAid { get; set; }
        public int MorseIvHeparinLock { get; set; }
        public int MorseGait { get; set; }
        public int MorseMentalStatus { get; set; }
        public int MorseTotal { get; set; }
        public string MorseRisk { get; set; } = null!;

        public int BradenSensoryPerception { get; set; }
        public int BradenMoisture { get; set; }
        public int BradenActivity { get; set; }
        public int BradenMobility { get; set; }
        public int BradenNutrition { get; set; }
        public int BradenFrictionShear { get; set; }
        public int BradenTotal { get; set; }
        public string BradenRisk { get; set; } = null!;

        public int MustBmiScore { get; set; }
        public int MustWeightLossScore { get; set; }
        public int MustAcuteDiseaseScore { get; set; }
        public int MustTotal { get; set; }
        public string MustRisk { get; set; } = null!;

        public string? Notes { get; set; }
    }
}
