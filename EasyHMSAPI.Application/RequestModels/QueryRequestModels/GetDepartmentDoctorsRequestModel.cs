using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDepartmentDoctorsRequestModel : IRequest<GetDepartmentDoctorsResponseModel>
    {
        public Guid DepartmentId { get; set; }
        public Guid HospitalId { get; set; } // Added hospitalId
    }
}
