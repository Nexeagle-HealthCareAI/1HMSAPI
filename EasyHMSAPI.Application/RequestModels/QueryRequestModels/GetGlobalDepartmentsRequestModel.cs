using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetGlobalDepartmentsRequestModel : IRequest<GetGlobalDepartmentsResponseModel>
    {
    }
}
