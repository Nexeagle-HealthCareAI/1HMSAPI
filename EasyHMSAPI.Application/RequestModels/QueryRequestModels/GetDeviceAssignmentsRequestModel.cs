using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDeviceAssignmentsRequestModel : IRequest<GetDeviceAssignmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
