using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class SendPathologyLineToExternalLabCommand : IRequest<bool>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public Guid OrderLineId { get; set; }
        public Guid? ExternalLabId { get; set; }
        public string? ExternalLabRefNo { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
