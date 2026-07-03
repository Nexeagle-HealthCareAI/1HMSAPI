using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // Partial draft — whatever's already captured elsewhere, pre-filled for review/override.
    // Lab-only fields (sodium, potassium, creatinine, hematocrit, WBC, pH) are always null here —
    // there's no structured lab-results system to pull them from.
    [ExcludeFromCodeCoverage]
    public class GetApacheIIAutoFillResponseModel
    {
        public decimal? Temperature { get; set; }
        public int? MapValue { get; set; }
        public int? HeartRate { get; set; }
        public int? RespiratoryRate { get; set; }
        public int? GcsTotal { get; set; }
        public int? AgeYears { get; set; }
        public DateTime? SourceVitalRecordedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetApacheIIScoreHistoryResponseModel
    {
        public List<ApacheIIScoreDataModel> Scores { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ApacheIIScoreDataModel
    {
        public Guid ApacheIIScoreId { get; set; }
        public int TotalScore { get; set; }
        public string ChronicHealthCategory { get; set; } = null!;
        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }
        public string? Notes { get; set; }
    }
}
