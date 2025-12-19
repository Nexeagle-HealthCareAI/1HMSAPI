using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeletePrescriptionAttachmentRequestModel : IRequest<DeletePrescriptionAttachmentResponseModel>
    {
        public Guid AttachmentId { get; set; }
    }
}
