using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetGlobalDepartmentsRequestModel : IRequest<GetGlobalDepartmentsResponseModel>
    {
    }
}
