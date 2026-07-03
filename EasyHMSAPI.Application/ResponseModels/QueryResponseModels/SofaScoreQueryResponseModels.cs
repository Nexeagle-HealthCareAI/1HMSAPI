using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetSofaAutoFillResponseModel
    {
        public int? MapValue { get; set; }
        public int? GcsTotal { get; set; }
        public decimal? UrineOutputMlPerDay { get; set; }
        public DateTime? SourceVitalRecordedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSofaScoreHistoryResponseModel
    {
        public List<SofaScoreDataModel> Scores { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SofaScoreDataModel
    {
        public Guid SofaScoreId { get; set; }
        public int TotalScore { get; set; }
        public int RespiratoryScore { get; set; }
        public int CoagulationScore { get; set; }
        public int LiverScore { get; set; }
        public int CardiovascularScore { get; set; }
        public int CnsScore { get; set; }
        public int RenalScore { get; set; }
        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }
        public string? Notes { get; set; }
    }
}
