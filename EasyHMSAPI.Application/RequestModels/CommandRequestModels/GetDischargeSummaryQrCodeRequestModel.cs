using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Renders the discharge summary's WhatsApp-delivery QR (NexEagle logo centered), encoding
    // {WhatsAppBot:BaseUrl}/d/{AccessToken} -- scanning it lands the patient in WhatsApp, where
    // the bot delivers the actual PDF (see WHatspp Backened's GET /d/{access_token}).
    // A Command, not a Query: mints AccessToken here if it isn't already set, since this runs at
    // PDF-render time, one step BEFORE the existing upload flow's own (idempotent) mint check.
    [ExcludeFromCodeCoverage]
    public class GetDischargeSummaryQrCodeRequestModel : IRequest<GetDischargeSummaryQrCodeResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
