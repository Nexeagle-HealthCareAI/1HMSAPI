using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Sends a freshly added team member their login details (mobile + password) over the chosen
    /// channel(s). The password is supplied by the caller (the admin just set it) — it is never read
    /// back from storage, which only holds the hash. Triggered from the Quick Add success screen.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ShareCredentialsRequestModel : IRequest<ShareCredentialsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string FullName { get; set; } = null!;
        public string MobileNumber { get; set; } = null!;   // the login id
        public string? Email { get; set; }
        public string Password { get; set; } = null!;
        public string? RoleName { get; set; }

        public bool ViaWhatsApp { get; set; }
        public bool ViaEmail { get; set; }

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
