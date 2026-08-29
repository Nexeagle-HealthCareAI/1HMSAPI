using MediatR;
using System;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DecideHrLeaveRequestModel : IRequest<DecideHrLeaveResponseModel>
    {
        public Guid LeaveId { get; set; }
        public string Status { get; set; } = null!;
        public string? Reason { get; set; }
    }
}
