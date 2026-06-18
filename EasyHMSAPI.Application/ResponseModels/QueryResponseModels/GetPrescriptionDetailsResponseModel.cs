using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionDetailsResponseModel
    {
        public PrescriptionDetailsDataModel? Data { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PrescriptionDetailsDataModel
    {
        public Guid? PrescriptionId { get; set; }
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
        public PatientVitalsModel? VitalsJson { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? History { get; set; }
        public string? Comorbidity { get; set; }
        public string? Examination { get; set; }
        public string? SystemicExamination { get; set; }
        public string? Diagnosis { get; set; }
        public OrdersModel? Orders { get; set; }
        public List<MedicationModel>? Medications { get; set; }
        public List<NonPharmacologicalAdviceModel>? NonPharmacologicalAdvice { get; set; }
        public string? PrivateNotes { get; set; }
        public CertificateDataModel? Certificates { get; set; }
        public FollowUpModel? FollowUp { get; set; }
        public List<ImmunizationModel>? Immunizations { get; set; }
        // Doctor's custom fields — self-describing (key + label + value).
        public List<PrescriptionCustomFieldModel>? CustomFields { get; set; }
    }
}
