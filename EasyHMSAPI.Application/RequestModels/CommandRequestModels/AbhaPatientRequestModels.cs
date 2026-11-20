using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class MatchAbhaPatientRequestModel : IRequest<MatchAbhaPatientResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>From an OTP verification or ABHA creation. Read, not used up.</summary>
        public string? LinkToken { get; set; }
        /// <summary>Instead of a LinkToken: a profile shared by scanning the counter QR.</summary>
        public Guid? ProfileShareId { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RegisterAbhaPatientRequestModel : IRequest<RegisterAbhaPatientResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? LinkToken { get; set; }
        public Guid? ProfileShareId { get; set; }
        /// <summary>An existing patient to put this ABHA on. Empty registers a new patient.</summary>
        public string? PatientId { get; set; }
        // Editable on the form. Name, date of birth and gender are not here: they are the ABHA's.
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
