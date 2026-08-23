using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetWhatsAppEntryQrCodeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public byte[]? Content { get; set; }
        public string ContentType { get; set; } = "image/png";
    }
}
