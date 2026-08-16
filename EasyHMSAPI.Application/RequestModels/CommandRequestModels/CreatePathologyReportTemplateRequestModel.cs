using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePathologyReportTemplateRequestModel : IRequest<Guid>
    {
        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public string TemplateCode { get; set; } = null!;

        [Required]
        public string TemplateName { get; set; } = null!;

        public string? HeaderBlobPath { get; set; }
        
        public string LayoutJson { get; set; } = "{}";
        
        public string? FooterText { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;

        public string? LoggedInUserName { get; set; }
    }
}
