using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class IssueQueueTokenResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? TokenNo { get; set; }
        public string? Status { get; set; }
    }
}
