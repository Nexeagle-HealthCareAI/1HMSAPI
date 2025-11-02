using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorDashboardAppointmentDetailsResponseModel
    {
        public List<DoctorDashboardAppointmentDetail> Items { get; set; } = new List<DoctorDashboardAppointmentDetail>();
    }

    [ExcludeFromCodeCoverage]
    public class DoctorDashboardAppointmentDetail
    {
        public string? PatientId { get; set; }
        public string? PatientFullName { get; set; }
        public string? PatientMobile { get; set; }
        public string? PatientSex { get; set; }
        public short? PatientAgeYears { get; set; }
        public Guid AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string? FinalStatusCode { get; set; }
        public string? Reason { get; set; }
        public string? InsuranceId { get; set; }
        public string? PaymentMode { get; set; }
        public DateTime? LastStatusAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public TokenDetailsDataModel? TokenDetails { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TokenDetailsDataModel
    {
        public Guid TokenId { get; set; }
        public int TokenNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
