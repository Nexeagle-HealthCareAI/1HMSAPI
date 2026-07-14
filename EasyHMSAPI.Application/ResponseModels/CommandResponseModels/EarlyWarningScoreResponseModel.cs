using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordEarlyWarningScoreResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ScoreId { get; set; }
        public int? TotalScore { get; set; }
        public string? RiskBand { get; set; }
        // Medium/High risk band — the frontend prompts "Activate Rapid Response?" off this flag
        // rather than duplicating the threshold logic client-side.
        public bool EscalationRecommended { get; set; }
    }
}
