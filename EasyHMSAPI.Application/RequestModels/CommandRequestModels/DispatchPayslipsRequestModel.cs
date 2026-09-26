using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DispatchPayslipsRequestModel : IRequest<DispatchPayslipsResponseModel>
    {
        public Guid HrPayrollRunId { get; set; }
        /// <summary>Always the caller's own identity (set by the controller, never client-supplied); used to
        /// verify they may manage payroll at the run's hospital.</summary>
        public Guid LoggedInUserId { get; set; }
    }
}
