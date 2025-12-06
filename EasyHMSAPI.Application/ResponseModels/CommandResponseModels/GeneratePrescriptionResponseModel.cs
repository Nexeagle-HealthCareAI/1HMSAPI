using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GeneratePrescriptionResponseModel
    {
        public bool Success { get; set; }
        public Guid AppointmentId { get; set; }
        public GeneratePrescriptionDataModel? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GeneratePrescriptionDataModel
    {
        public PrescriptionTemplateModel? Template { get; set; }
        public PatientPrescriptionDataModel? PatientData { get; set; }
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
        public string? Sex { get; set; }
        public string? Address { get; set; }
        public string? Contact { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientVitalsModel
    {
        public BloodPressureModel? Bp { get; set; }
        public int Pulse { get; set; }
        public int TempC { get; set; }
        public int Spo2 { get; set; }
        public int HeightCm { get; set; }
        public int WeightKg { get; set; }
        public double Bmi { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BloodPressureModel
    {
        public int Sys { get; set; }
        public int Dia { get; set; }
    }
}
