using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadHealthLockerDocumentResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? DocumentId { get; set; }
        public string? FileUrl { get; set; }
    }
}
