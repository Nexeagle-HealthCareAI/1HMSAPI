using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteProfilePictureRequestModel : IRequest<DeleteProfilePictureResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
