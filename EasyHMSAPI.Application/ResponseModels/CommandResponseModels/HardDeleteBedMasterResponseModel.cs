using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HardDeleteBedMasterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BulkHardDeleteBedMasterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<Guid> Deleted { get; set; } = new();
        public List<BedDeleteFailure> Blocked { get; set; } = new();
    }
}
