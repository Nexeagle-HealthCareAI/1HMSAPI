using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class EnterPathologyResultCommand : IRequest<bool>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public Guid OrderLineId { get; set; }
        public string ResultValuesJson { get; set; } = "{}";
        public string? Interpretation { get; set; }

        // Mandatory when the line already has a report: the change is kept in PathologyResultHistory and the report is marked AMENDED.
        public string? AmendmentReason { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
