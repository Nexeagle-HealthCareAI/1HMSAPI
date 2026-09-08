using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DispatchPayslipsRequestModel : IRequest<DispatchPayslipsResponseModel>
    {
        public Guid HrPayrollRunId { get; set; }
    }
}
