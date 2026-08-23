using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class CreatePathologyOrderResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNo { get; set; }
    }
}
