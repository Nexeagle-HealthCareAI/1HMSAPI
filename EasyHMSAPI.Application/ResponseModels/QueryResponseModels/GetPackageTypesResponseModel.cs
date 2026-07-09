using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPackageTypesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PackageTypeDataModel> PackageTypes { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PackageTypeDataModel
    {
        public Guid PackageTypeId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public List<string> Components { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
