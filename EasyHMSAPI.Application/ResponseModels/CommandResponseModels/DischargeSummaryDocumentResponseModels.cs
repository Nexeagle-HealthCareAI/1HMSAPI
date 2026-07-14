using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadDischargeSummaryPdfResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Long random opaque token — the frontend builds the public view URL from this
        // (ApiBaseUrl + /public-discharge/{token}) and encodes it into the printed QR code.
        public string? AccessToken { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SendDischargeSummaryWhatsAppResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
