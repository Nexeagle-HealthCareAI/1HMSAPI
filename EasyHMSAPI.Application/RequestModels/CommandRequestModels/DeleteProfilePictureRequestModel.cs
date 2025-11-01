using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeleteProfilePictureRequestModel : IRequest<DeleteProfilePictureResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
