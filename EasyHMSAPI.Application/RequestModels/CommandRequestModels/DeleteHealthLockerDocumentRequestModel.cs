using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — the handler's
    // lookup is scoped to (DocumentId, Mobile) together, so a patient can never delete someone
    // else's document even by guessing a DocumentId.
    [ExcludeFromCodeCoverage]
    public class DeleteHealthLockerDocumentRequestModel : IRequest<DeleteHealthLockerDocumentResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
    }
}
