using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetWardListResponseModel
    {
        public List<WardListItem> Wards { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class WardListItem
    {
        public string WardCode { get; set; } = null!;
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public int BedCount { get; set; }
    }
}
