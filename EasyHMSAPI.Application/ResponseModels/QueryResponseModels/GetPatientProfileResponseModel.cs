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
        public short? Age { get; set; }
        public string? AgeUnit { get; set; }
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
        // Clinical / contact fields (already stored on registration, now surfaced for the doctor view).
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public string? Block { get; set; }
        public string? AlternateMobile { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
    }
}