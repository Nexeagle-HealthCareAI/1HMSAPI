using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class CancelPathologyOrderCommand : IRequest<ResponseModels.CommandResponseModels.CancelPathologyOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
