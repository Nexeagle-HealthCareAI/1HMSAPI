using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionDrawingsResponseModel
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public int? DrawingCount { get; set; }
        public List<PrescriptionDrawingDataModel>? Drawings { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PrescriptionDrawingDataModel
    {
        public Guid DrawingId { get; set; }
        public string? Label { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public int SequenceNo { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
