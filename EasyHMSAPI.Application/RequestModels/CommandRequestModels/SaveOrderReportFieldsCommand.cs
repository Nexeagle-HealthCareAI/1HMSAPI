using MediatR;
using System;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class SaveOrderReportFieldsCommand : IRequest<bool>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public string ReportFieldValuesJson { get; set; } = "{}";

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
