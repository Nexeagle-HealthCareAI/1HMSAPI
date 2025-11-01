using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetDepartmentsRequestModel : IRequest<GetDepartmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
