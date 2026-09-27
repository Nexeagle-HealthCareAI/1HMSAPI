using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetHrBiometricDevicesRequestModel : IRequest<GetHrBiometricDevicesResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    /// <summary>PINs that have scanned at the hospital's devices but aren't linked to an employee yet.</summary>
    [ExcludeFromCodeCoverage]
    public class GetUnmappedDeviceUsersRequestModel : IRequest<GetUnmappedDeviceUsersResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    /// <summary>The employee-to-PIN links that already exist at the hospital.</summary>
    [ExcludeFromCodeCoverage]
    public class GetEmployeeDeviceUsersRequestModel : IRequest<GetEmployeeDeviceUsersResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
