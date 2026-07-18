using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentRequestModel : IRequest<GetPublicAppointmentResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }
}
