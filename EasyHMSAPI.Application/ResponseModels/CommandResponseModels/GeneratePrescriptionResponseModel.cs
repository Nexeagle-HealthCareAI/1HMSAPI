using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GeneratePrescriptionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid AppointmentId { get; set; }
        public DateTime? ValidUptoDate { get; set; }
        public GeneratePrescriptionDataModel? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GeneratePrescriptionDataModel
    {
        public PrescriptionTemplateModel? Template { get; set; }
        public PatientPrescriptionDataModel? PatientData { get; set; }
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
        public List<PrescriptionCustomFieldModel>? CustomFields { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PrescriptionTemplateModel
    {
        public Guid? PrescriptionSettingsId { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public int HeaderHeight { get; set; }
        public int FooterHeight { get; set; }
        public int ContentLeftMargin { get; set; }
        public int ContentRightMargin { get; set; }
        public bool OverFlowPage { get; set; }
        public string? FontFamily { get; set; }
        public int FontSize { get; set; }
        public string? FontWeight { get; set; }
        public string? TextColour { get; set; }
        public string? Uri { get; set; }
        public int ValidUpto { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientPrescriptionDataModel
    {
        public List<PatientDetailsModel>? PatientDetails { get; set; }
        public PatientVitalsModel? Vitals { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientDetailsModel
    {
        public string? PatientId { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? Sex { get; set; }
        public string? Address { get; set; }
        public string? Contact { get; set; }
        public string? Mobile { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? InsuranceId { get; set; }
        public string? ReferrerName { get; set; }
        public string? ReferrerRelation { get; set; }
        // Guardian / relative (patient-level, separate from medical referral).
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientVitalsModel
    {
        public BloodPressureModel? Bp { get; set; }
        public double Pulse { get; set; }
        public double TempC { get; set; }
        public double Spo2 { get; set; }
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public double Bmi { get; set; }
        public double RespiratoryRate { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BloodPressureModel
    {
        public double Sys { get; set; }
        public double Dia { get; set; }
    }
}
