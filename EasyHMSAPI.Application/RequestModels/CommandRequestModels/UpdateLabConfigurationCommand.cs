using MediatR;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateLabConfigurationCommand : IRequest<bool>
    {
        [Required]
        public Guid HospitalId { get; set; }

        public bool AutoBillOnOrder { get; set; }
        public string? DefaultReportHeaderBlob { get; set; }
        public string? DefaultReportFooterText { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
