using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadAdmissionDocumentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DocumentId { get; set; }
        public string? FileUrl { get; set; }
    }
}
