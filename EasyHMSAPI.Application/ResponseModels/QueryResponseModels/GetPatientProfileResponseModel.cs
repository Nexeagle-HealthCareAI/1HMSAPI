using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientProfileResponseModel
    {
        public Guid RegistrationId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? AgeYears { get; set; }
        public string? Sex { get; set; }
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? InsuranceId { get; set; }
        public string? PaymentMode { get; set; }
        public DateTime RegisteredAt { get; set; }
        public Guid? RegisteredBy { get; set; }
    }
}