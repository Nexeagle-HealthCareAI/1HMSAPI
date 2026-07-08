using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class BulkDeleteBedMasterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<Guid> Deactivated { get; set; } = new();
        public List<BedDeleteFailure> Blocked { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class BedDeleteFailure
    {
        public Guid BedId { get; set; }
        public string? BedCode { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
