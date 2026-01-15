using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadVisitSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Url { get; set; }
        public bool IsSentViaWhatsApp { get; set; }
    }
}
