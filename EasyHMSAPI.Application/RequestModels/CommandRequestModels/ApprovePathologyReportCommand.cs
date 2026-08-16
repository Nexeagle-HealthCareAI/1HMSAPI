using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class ApprovePathologyReportCommand : IRequest<bool>
    {
        public Guid HospitalId { get; set; }
        public Guid ReportId { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
