using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders the prescription's WhatsApp-delivery QR (NexEagle logo centered), encoding
    // {WhatsAppBot:BaseUrl}/rx/{AttachmentId}. Deliberately takes AttachmentId as input rather
    // than looking one up: the frontend generates this ID client-side BEFORE the
    // PrescriptionAttachment row exists (the QR has to be embedded into the PDF that then gets
    // uploaded to CREATE that row), so there's nothing to look up yet at generation time -- this
    // is a pure, stateless "encode this URL" operation, no DB access at all.
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionAttachmentQrCodeRequestModel : IRequest<GetPrescriptionAttachmentQrCodeResponseModel>
    {
        public Guid AttachmentId { get; set; }
    }
}
