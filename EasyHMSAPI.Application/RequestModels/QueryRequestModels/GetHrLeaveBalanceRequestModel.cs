using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrLeaveBalanceRequestModel : IRequest<GetHrLeaveBalanceResponseModel>
    {
        public Guid EmployeeId { get; set; }
        public int? Year { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
