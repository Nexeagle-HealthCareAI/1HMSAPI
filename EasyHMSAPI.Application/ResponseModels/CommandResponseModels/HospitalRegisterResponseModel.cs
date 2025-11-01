using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class HospitalRegisterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? HospitalUserId { get; set; }
    }
} 