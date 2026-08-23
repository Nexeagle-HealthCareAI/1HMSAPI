using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Backs the fully anonymous "view/deliver prescription" link -- no staff JWT, no API key,
    // keyed only by AttachmentId (an unguessable GUID, the same "opaque id as the access
    // control" pattern already used by GET public/appointments/{appointmentId}). This is what
    // the printed QR code and the WhatsApp bot's GET /rx/{id} both resolve through.
    [ExcludeFromCodeCoverage]
    public class GetPublicPrescriptionAttachmentRequestModel : IRequest<GetPublicPrescriptionAttachmentResponseModel>
    {
        public Guid AttachmentId { get; set; }
    }
}
