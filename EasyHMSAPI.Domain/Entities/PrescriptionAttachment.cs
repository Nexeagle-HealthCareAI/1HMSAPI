using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionAttachment
    {
        public Guid AttachmentId { get; set; }
        public Guid ApptId { get; set; }
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? ReportType { get; set; }
        public string? StorageUrl { get; set; }
        public string? FileName { get; set; }
        public string? Notes { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
