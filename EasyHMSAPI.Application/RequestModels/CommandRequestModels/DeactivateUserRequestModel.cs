using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeactivateUserRequestModel : MediatR.IRequest<DeactivateUserResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }

        // Audit hint from the client; never trusted for authorization (see CallerUserId).
        public Guid PerformedByUserId { get; set; }

        // The signed-in caller, resolved from the JWT by the controller. Drives authorization + audit.
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
