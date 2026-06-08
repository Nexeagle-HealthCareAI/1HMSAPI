using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Create a hospital chain owned by the caller. The caller's existing standalone hospital(s)
    /// become the chain's first member(s). Only an owner (Admin/AdminDoctor) should reach this.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CreateHospitalChainRequestModel : IRequest<CreateHospitalChainResponseModel>
    {
        public string Name { get; set; } = null!;
        [JsonIgnore]
        public Guid OwnerUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
