using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class CreateHrEmployeeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public Guid HrEmployeeId { get; set; }
        public string EmployeeCode { get; set; } = null!;
    }
}
