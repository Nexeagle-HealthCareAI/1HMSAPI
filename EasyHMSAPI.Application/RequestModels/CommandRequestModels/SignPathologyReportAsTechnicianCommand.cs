using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class SignPathologyReportAsTechnicianCommand : IRequest<bool>
    {
        public Guid HospitalId { get; set; }
        public Guid ReportId { get; set; }
        public string TechnicianRegNo { get; set; } = null!;

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
