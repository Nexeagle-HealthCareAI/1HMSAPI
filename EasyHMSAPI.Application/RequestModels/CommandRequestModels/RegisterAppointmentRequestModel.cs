using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class RegisterAppointmentRequestModel : IRequest<RegisterAppointmentResponseModel>
    {
        public Guid? AppointmentId { get; set; }
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public bool AllocateToken { get; set; }
        public Patient? Patient { get; set; }
        public Guid DoctorId { get; set; }
        public DateTime ApptDate { get; set; }
        public DateTime? StartAt { get; set; }
        public string? Reason { get; set; }
        public int? SlotTimeInMinutes { get; set; }
        public Guid? UserId { get; set; }
        public Guid? ReferredByReferrerId { get; set; }
        public string? ReferrerRelation { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class Patient
    {
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? Sex { get; set; }
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? InsuranceId { get; set; }
        public string? PaymentMode { get; set; }
        public string? PatientId { get; set; }

        // Optional extra demographics from the booking popup.
        public string? BloodGroup { get; set; }
        public string? Block { get; set; }
        public string? AlternateMobile { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Guardian / relative info (separate from medical referrals on the appointment).
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
    }
}
