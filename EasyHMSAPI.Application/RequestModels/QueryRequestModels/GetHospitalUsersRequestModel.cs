using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalUsersRequestModel : MediatR.IRequest<GetHospitalUsersResponseModel?>
    {
        public Guid UserId { get; }

        public GetHospitalUsersRequestModel(Guid userId)
        {
            UserId = userId;
        }
    }
} 