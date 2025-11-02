using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationValidateRequestModel : IRequest<InvitationValidateResponseModel>
    {
        public string Token { get; set; } = null!;
    }
}
