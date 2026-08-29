using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPayslipsByRunRequestModel : IRequest<GetPayslipsByRunResponseModel>
    {
        public Guid HrPayrollRunId { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
