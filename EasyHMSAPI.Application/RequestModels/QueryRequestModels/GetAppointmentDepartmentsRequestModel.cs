using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAppointmentDepartmentsRequestModel : IRequest<GetAppointmentDepartmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
