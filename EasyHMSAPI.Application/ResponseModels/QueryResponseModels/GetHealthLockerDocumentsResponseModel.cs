using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHealthLockerDocumentsResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<HealthLockerDocumentItem> Documents { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HealthLockerDocumentItem
    {
        public Guid DocumentId { get; set; }
        public Guid? ApptId { get; set; }
        public string? DocumentType { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
