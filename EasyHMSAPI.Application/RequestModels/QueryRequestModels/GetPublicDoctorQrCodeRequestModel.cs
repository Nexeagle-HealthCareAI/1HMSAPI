using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders a doctor's WhatsApp-booking QR (NexEagle logo centered), encoding
    // {WhatsAppBot:BaseUrl}/doc/{DoctorId} -- a pure, stateless "encode this URL" operation,
    // same posture as GetPrescriptionAttachmentQrCodeRequestModel. DoctorId isn't validated
    // here: this is rendered on the doctor's own already-resolved public profile page, and
    // the bot's GET /doc/{doctorId} route re-validates independently before ever opening
    // WhatsApp anyway.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorQrCodeRequestModel : IRequest<GetPublicDoctorQrCodeResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
