using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionDrawing
    {
        public Guid DrawingId { get; set; }
        public Guid ApptId { get; set; }
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public string? Label { get; set; }
        public string? StorageUrl { get; set; }
        public string? FileName { get; set; }
        public int SequenceNo { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
