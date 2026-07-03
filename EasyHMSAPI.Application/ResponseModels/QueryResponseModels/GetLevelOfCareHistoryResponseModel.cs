using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetLevelOfCareHistoryResponseModel
    {
        public List<IcuLevelOfCareDataModel> History { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IcuLevelOfCareDataModel
    {
        public Guid IcuLevelOfCareId { get; set; }
        public string Level { get; set; } = null!;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }
    }
}
