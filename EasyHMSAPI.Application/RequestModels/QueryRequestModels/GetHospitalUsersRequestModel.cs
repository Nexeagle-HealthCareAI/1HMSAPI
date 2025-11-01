using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHospitalUsersRequestModel : MediatR.IRequest<GetHospitalUsersResponseModel?>
    {
        public Guid UserId { get; }

        public GetHospitalUsersRequestModel(Guid userId)
        {
            UserId = userId;
        }
    }
} 