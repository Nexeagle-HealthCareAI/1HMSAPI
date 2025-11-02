using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeactivateUserRequestModel : MediatR.IRequest<DeactivateUserResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public Guid PerformedByUserId { get; set; }
    }
}
