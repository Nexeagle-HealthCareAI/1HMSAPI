using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePrescriptionAttachmentRequestModel : IRequest<DeletePrescriptionAttachmentResponseModel>
    {
        public Guid AttachmentId { get; set; }
    }
}
