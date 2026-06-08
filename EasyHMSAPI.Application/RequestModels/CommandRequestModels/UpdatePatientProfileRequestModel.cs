using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePatientProfileRequestModel : IRequest<UpdatePatientProfileResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public string PatientId { get; set; } = string.Empty;
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
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }
}