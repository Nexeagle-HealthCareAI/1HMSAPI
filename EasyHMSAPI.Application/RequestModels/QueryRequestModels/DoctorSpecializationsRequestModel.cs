using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorSpecializationsRequestModel : IRequest<DoctorSpecializationsResponseModel>
    {
        public Guid DepartmentId { get; set; }
        public Guid? HospitalId { get; set; }
        public bool IncludeGlobal { get; set; } = true;
    }
}
