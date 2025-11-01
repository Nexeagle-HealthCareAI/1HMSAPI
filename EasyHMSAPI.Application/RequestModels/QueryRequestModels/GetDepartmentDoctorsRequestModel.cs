using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetDepartmentDoctorsRequestModel : IRequest<GetDepartmentDoctorsResponseModel>
    {
        public Guid DepartmentId { get; set; }
    }
}
