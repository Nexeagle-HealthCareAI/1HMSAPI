using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SetOrResetPasswordRequestModel : IRequest<SetOrResetPasswordResponseModel>
    {
        public Guid UserId { get; set; }
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        [JsonIgnore]
        public string Scope { get; set; } = string.Empty;
    }
}
