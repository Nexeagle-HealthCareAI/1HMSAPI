using MediatR;
using System;
using System.Text.Json.Serialization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DecideHrLeaveRequestModel : IRequest<DecideHrLeaveResponseModel>
    {
        public Guid LeaveId { get; set; }
        public string Status { get; set; } = null!;
        public string? Reason { get; set; }

        // Who approved/rejected this -- always the caller's own identity, never client-supplied.
        [JsonIgnore]
        public Guid ApprovedByUserId { get; set; }
    }
}
