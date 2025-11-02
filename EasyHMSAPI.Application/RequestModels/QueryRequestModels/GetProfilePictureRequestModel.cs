using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetProfilePictureRequestModel : IRequest<GetProfilePictureResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
