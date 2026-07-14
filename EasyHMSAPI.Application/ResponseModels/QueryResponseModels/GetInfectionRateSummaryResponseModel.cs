using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInfectionRateSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<InfectionRateSummaryItem> Rates { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class InfectionRateSummaryItem
    {
        public string DeviceType { get; set; } = null!;
        public string InfectionType { get; set; } = null!;
        public int InfectionCount { get; set; }
        public decimal DeviceDays { get; set; }
        public decimal? RatePer1000DeviceDays { get; set; }
    }
}
