using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DecideCreditApprovalResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Status { get; set; }
        public DateTime? DecidedAt { get; set; }
    }
}
