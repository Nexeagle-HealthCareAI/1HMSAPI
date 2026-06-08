using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientTimelineResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PatientTimelineDataModel>? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientTimelineDataModel
    {
        public string? PatientID { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public List<TimelineAppointmentModel>? TimelineData { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TimelineAppointmentModel
    {
        public Guid? ApptID { get; set; }
        public DateTime? AppDate { get; set; }
        public string? Status { get; set; }
        public Guid? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public List<StatusHistoryModel>? StatusJsonHistory { get; set; }
        public PatientVitalsModel? VitalsJson { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? History { get; set; }
        public string? Comorbidity { get; set; }
        public string? Examination { get; set; }
        public string? Diagnosis { get; set; }
        public OrdersModel? Orders { get; set; }
        public List<MedicationModel>? Medications { get; set; }
        public List<NonPharmacologicalAdviceModel>? NonPharmacologicalAdvice { get; set; }
        public string? PrivateNotes { get; set; }
        public CertificateDataModel? Certificates { get; set; }
        public FollowUpModel? FollowUp { get; set; }
        public List<ImmunizationModel>? Immunizations { get; set; }
        public List<AttachmentModel>? Attachments { get; set; }
        public List<PrescriptionCustomFieldModel>? CustomFields { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class StatusHistoryModel
    {
        public string? Status { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AttachmentModel
    {
        public Guid? AttachmentId { get; set; }
        public string? ReportType { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
