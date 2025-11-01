using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModel
{
    public class HospitalUpdateRequestModel : MediatR.IRequest<HospitalUpdateResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? TimeZone { get; set; }
    }

} 