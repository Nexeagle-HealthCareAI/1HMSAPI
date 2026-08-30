using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // Partial draft — whatever's already captured elsewhere, pre-filled for review/override.
    // Sodium/potassium/creatinine/hematocrit/WBC are pulled from the patient's most recent
    // APPROVED PathologyReport when one exists (see PathologyLabValueResolver) -- ArterialPh/
    // FiO2/PaO2 stay null since ABG isn't one of the seeded catalog panels.
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

        public decimal? SerumSodium { get; set; }
        public decimal? SerumPotassium { get; set; }
        public decimal? SerumCreatinine { get; set; }
        public decimal? Hematocrit { get; set; }
        public decimal? Wbc { get; set; }
        public DateTime? SourceLabReportApprovedAt { get; set; }
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
