using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders the generic "chat with us on WhatsApp" QR (NexEagle logo centered) used e.g. at
    // the bottom of the Doctor Dekho homepage -- encodes {WhatsAppBot:BaseUrl}/start, which
    // just opens a fresh conversation (no code to resolve, unlike every other QR endpoint in
    // this codebase). Takes no parameters -- the image is identical on every call, so callers
    // are expected to cache it rather than re-fetch per page view.
    [ExcludeFromCodeCoverage]
    public class GetWhatsAppEntryQrCodeRequestModel : IRequest<GetWhatsAppEntryQrCodeResponseModel>
    {
    }
}
