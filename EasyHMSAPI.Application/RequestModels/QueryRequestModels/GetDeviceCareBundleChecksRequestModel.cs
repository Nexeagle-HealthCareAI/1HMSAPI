using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDeviceCareBundleChecksRequestModel : IRequest<GetDeviceCareBundleChecksResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DeviceAssignmentId { get; set; }
    }
}
