using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Issues a new per-hospital public API key (e.g. for the Nexeagle booking website). The raw
    // key is returned once in the response and never persisted — only its hash is stored.
    [ExcludeFromCodeCoverage]
    public class CreatePublicApiClientRequestModel : IRequest<CreatePublicApiClientResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? ClientName { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    // Deactivates a key — it stops authenticating immediately (PublicApiKeyFilter only matches
    // IsActive rows). No delete, so the row remains for audit/history.
    [ExcludeFromCodeCoverage]
    public class RevokePublicApiClientRequestModel : IRequest<RevokePublicApiClientResponseModel>
    {
        public Guid ApiClientId { get; set; }
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
