using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDischargeSummaryQrCodeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public byte[]? Content { get; set; }
        public string ContentType { get; set; } = "image/png";
    }
}
