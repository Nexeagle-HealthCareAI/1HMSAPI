using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorSpecializationsRequestModel : IRequest<DoctorSpecializationsResponseModel>
    {
        public Guid DepartmentId { get; set; }
        public Guid? HospitalId { get; set; }
        public bool IncludeGlobal { get; set; } = true;
    }
}
