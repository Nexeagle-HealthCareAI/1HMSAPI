using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetEarlyWarningAutoFillResponseModel
    {
        public int? RespiratoryRate { get; set; }
        public decimal? Spo2 { get; set; }
        public int? SystolicBp { get; set; }
        public int? Pulse { get; set; }
        public decimal? TemperatureC { get; set; }
        public DateTime? SourceVitalRecordedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetEarlyWarningScoreHistoryResponseModel
    {
        public List<EarlyWarningScoreDataModel> Scores { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class EarlyWarningScoreDataModel
    {
        public Guid ScoreId { get; set; }
        public int TotalScore { get; set; }
        public string RiskBand { get; set; } = null!;
        public int RrScore { get; set; }
        public int Spo2Score { get; set; }
        public int O2Score { get; set; }
        public int BpScore { get; set; }
        public int PulseScore { get; set; }
        public int ConsciousnessScore { get; set; }
        public int TempScore { get; set; }
        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }
        public string? Notes { get; set; }
    }
}
