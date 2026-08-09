using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    /// <summary>Carries a raw image/PDF payload (§10 Generate QR Code, §11 Generate ABHA Card) back
    /// through MediatR so the controller can return it as a file rather than JSON.</summary>
    [ExcludeFromCodeCoverage]
    public class AbdmBinaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public byte[]? Content { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
