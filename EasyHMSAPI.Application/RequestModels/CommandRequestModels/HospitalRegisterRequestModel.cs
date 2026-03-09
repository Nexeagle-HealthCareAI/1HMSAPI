using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModel
{
    [ExcludeFromCodeCoverage]
    public class HospitalRegisterRequestModel : MediatR.IRequest<HospitalRegisterResponseModel>
    {
        public Guid UserId { get; set; }
        public string? Name { get; set; } = null!;
        public string? Type { get; set; } = null!;
        public string? RegistrationNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string Contact { get; set; } = null!;
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string Location { get; set; } = null!;
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string Pincode { get; set; } = null!;
        public string? TimeZone { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
} 