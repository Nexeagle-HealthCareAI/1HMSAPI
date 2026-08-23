using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetQueueTokenStatusRequestModel : IRequest<GetQueueTokenStatusResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }
}
