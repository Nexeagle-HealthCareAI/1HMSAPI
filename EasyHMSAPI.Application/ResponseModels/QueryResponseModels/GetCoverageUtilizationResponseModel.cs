using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetCoverageUtilizationResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public string? PayerType { get; set; }
        public decimal? SanctionedAmount { get; set; }
        public decimal EffectiveSanctionedAmount { get; set; }
        public decimal RunningTotal { get; set; }
        public decimal? UtilizationPercent { get; set; }
        public bool IsApproachingLimit { get; set; }

        public DateTime? EnhancementRequestedAt { get; set; }
        public string? EnhancementRequestedBy { get; set; }
        public decimal? EnhancedSanctionedAmount { get; set; }
        public DateTime? EnhancementApprovedAt { get; set; }
        public string? EnhancementApprovedBy { get; set; }
    }
}
