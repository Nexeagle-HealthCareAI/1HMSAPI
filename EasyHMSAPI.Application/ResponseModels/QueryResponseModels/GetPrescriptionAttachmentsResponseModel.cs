using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionAttachmentsResponseModel
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public int? AttachmentCount { get; set; }
        public List<AttachmentsDataModel>? Attachments { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class  AttachmentsDataModel
    {
        public Guid AttachmentId { get; set; }
        public string? ReportType { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
