using MediatR;
using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrEmployeesRequestModel : IRequest<GetHrEmployeesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? DepartmentId { get; set; }
        public string? EmploymentType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid LoggedInUserId { get; set; }
    }
}
