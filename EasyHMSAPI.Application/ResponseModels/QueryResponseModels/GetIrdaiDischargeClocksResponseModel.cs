using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetIrdaiDischargeClocksResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PayerType { get; set; }
        public List<IrdaiMilestoneModel> Milestones { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IrdaiMilestoneModel
    {
        public string Key { get; set; } = null!;
        public string Label { get; set; } = null!;
        public DateTime? At { get; set; }
        public int? DurationFromPreviousMinutes { get; set; }
    }
}
