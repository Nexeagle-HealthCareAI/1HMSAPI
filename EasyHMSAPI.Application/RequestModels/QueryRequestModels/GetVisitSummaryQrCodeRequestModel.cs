using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders the structured e-prescription's WhatsApp-delivery QR (NexEagle logo centered),
    // encoding {WhatsAppBot:BaseUrl}/rxv/{AppointmentId}. AppointmentId is known upfront (unlike
    // PrescriptionAttachment.AttachmentId for InkRx/manual uploads), so unlike that flow, no
    // pre-generated id needs to be threaded through -- this can be a plain, immediate lookup.
    [ExcludeFromCodeCoverage]
    public class GetVisitSummaryQrCodeRequestModel : IRequest<GetVisitSummaryQrCodeResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }
}
