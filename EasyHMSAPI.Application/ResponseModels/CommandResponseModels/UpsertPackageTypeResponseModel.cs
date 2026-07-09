using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPackageTypeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? PackageTypeId { get; set; }
    }
}
