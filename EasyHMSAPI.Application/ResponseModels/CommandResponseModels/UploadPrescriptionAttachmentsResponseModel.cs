using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadPrescriptionAttachmentsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AttachmentId { get; set; }
        public string? FileUrl { get; set; }
    }
}
