using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class BulkCreateBedMasterResponseModel
    {
        public bool Success { get; set; }
        public int CreatedCount { get; set; }
        public string? FirstBedCode { get; set; }
        public string? LastBedCode { get; set; }
        public string? Message { get; set; }
    }
}
