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
        public string? GstIn { get; set; }
        public string? PanNumber { get; set; }
        public string? NabhNabl { get; set; }
        // When set, onboard this hospital into an existing chain (caller must be the chain owner).
        public Guid? ChainId { get; set; }
        // Optional referral code entered at registration. Validated against CMSAPI's referral code
        // catalog; an invalid/expired/already-used code never blocks registration -- see
        // HospitalRegisterResponseModel.ReferralCodeApplied for the soft feedback shown to the user.
        public string? ReferralCode { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
} 