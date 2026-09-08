using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrEmployeesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public List<HrEmployeeDto> Employees { get; set; } = new List<HrEmployeeDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class HrEmployeeDto
    {
        public Guid HrEmployeeId { get; set; }
        public string EmployeeCode { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string ContactNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string EmploymentType { get; set; } = null!;
        public string Designation { get; set; } = null!;
        public string DepartmentName { get; set; } = null!;
        public DateOnly DateOfJoining { get; set; }
        public string Status { get; set; } = null!;
    }
}
