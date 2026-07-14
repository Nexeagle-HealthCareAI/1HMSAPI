using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionDocumentsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int DocumentCount { get; set; }
        public List<AdmissionDocumentItem> Documents { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionDocumentItem
    {
        public Guid DocumentId { get; set; }
        public string? DocumentName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? StorageUrl { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
