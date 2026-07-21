using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentDocumentsResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid AppointmentId { get; set; }
        public List<PublicAppointmentDocument> Documents { get; set; } = new();
    }

    // Patient-safe subset of PrescriptionAttachment — no internal HospitalId/DoctorId/PatientId,
    // just what the Doctor Dekho UI needs to show and open the file.
    [ExcludeFromCodeCoverage]
    public class PublicAppointmentDocument
    {
        public Guid AttachmentId { get; set; }
        public string? ReportType { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
