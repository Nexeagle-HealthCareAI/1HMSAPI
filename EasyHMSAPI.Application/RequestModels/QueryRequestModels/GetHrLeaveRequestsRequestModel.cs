using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrLeaveRequestsRequestModel : IRequest<GetHrLeaveRequestsResponseModel>
    {
        public Guid? HospitalId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? Status { get; set; }
    }
}
