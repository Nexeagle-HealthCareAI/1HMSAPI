using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ReactivateUserRequestModel : IRequest<ReactivateUserResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
