using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordEnhancementRequestResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordEnhancementApprovalResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
