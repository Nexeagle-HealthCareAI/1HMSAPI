using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // WhatsApp's status callback for one Health Wiki message (delivered, read or failed), passed on by the WhatsApp webhook service.
    [ExcludeFromCodeCoverage]
    public class RecordHealthWikiDeliveryRequestModel : IRequest<RecordHealthWikiDeliveryResponseModel>
    {
        public string? MessageId { get; set; }
        // WhatsApp's word: sent | delivered | read | failed
        public string? Status { get; set; }
        // WhatsApp's reason, on a failure.
        public string? Detail { get; set; }
    }
}
