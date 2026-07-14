using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A general-purpose document uploaded against an admission (insurance card, ID proof,
    /// referral letter, scanned report, etc.), listed on the Patient Workspace's Documents tab.
    /// Insert/delete only, never updated in place. StorageObjectKey is the S3/MinIO blob key used
    /// to re-sign a fresh URL on every read (IBlobStorageService.RefreshUrlAsync); StorageUrl is
    /// just the last-signed URL.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionDocument")]
    public class AdmissionDocument
    {
        [Key]
        public Guid DocumentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }

        public string DocumentName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }
        public string StorageObjectKey { get; set; } = string.Empty;
        public string StorageUrl { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
