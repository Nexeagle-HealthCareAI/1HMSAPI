using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientAppointmentDetailsResponseModel
    {
        public List<AppointmentDetail> Items { get; set; } = new List<AppointmentDetail>();
        public int TotalAppointments => Items.Count;
    }

    [ExcludeFromCodeCoverage]
    public class AppointmentDetail
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public string? PatientFullName { get; set; }
        public string? PatientMobile { get; set; }
        public string? PatientSex { get; set; }
        public int? PatientAgeYears { get; set; }
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public Guid DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public DateTime AppointmentDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string? FinalStatusCode { get; set; }
        public string? Reason { get; set; }
        public string? InsuranceId { get; set; }
        public string? PaymentMode { get; set; }
        public string? AppointmentType { get; set; }
        public DateTime LastStatusAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public TokenDetail? Token { get; set; }
        public List<StatusHistoryModel>? StatusJsonHistory { get; set; }

        // OPD consult billing status for this appointment (drives the Bill button + success modal).
        public Guid? EncounterId { get; set; }
        public bool ConsultCharged { get; set; }
        public bool ConsultPaid { get; set; }
        public decimal ConsultAmount { get; set; }
        public string? ConsultReceiptNo { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TokenDetail
    {
        public Guid TokenId { get; set; }
        public int? TokenNumber { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
