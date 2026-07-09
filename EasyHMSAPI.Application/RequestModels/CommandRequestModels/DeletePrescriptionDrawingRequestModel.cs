using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePrescriptionDrawingRequestModel : IRequest<DeletePrescriptionDrawingResponseModel>
    {
        public Guid DrawingId { get; set; }
    }
}
