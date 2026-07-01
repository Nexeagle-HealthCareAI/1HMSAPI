using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class StartRestraintResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? RestraintOrderId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReleaseRestraintResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? RestraintOrderId { get; set; }
    }
}
