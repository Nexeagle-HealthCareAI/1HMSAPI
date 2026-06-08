using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Resets an existing team member's password to a fresh random temporary one and returns it (once)
    /// so the admin can re-share login details. Used when the original password is no longer known
    /// (storage only keeps the hash). The temp password is returned to the caller only; never persisted in plaintext.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResetCredentialsRequestModel : IRequest<ResetCredentialsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
