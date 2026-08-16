using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePathologyTestRequestModel : IRequest<Guid>
    {
        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public string TestCode { get; set; } = null!;

        [Required]
        public string TestName { get; set; } = null!;

        public string? Category { get; set; }

        public Guid? ChargeId { get; set; }

        public string? SampleType { get; set; }
        public string? ContainerType { get; set; }

        public string? ParameterSchemaJson { get; set; }

        public Guid? DefaultTemplateId { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public string? LoggedInUserName { get; set; }
    }
}
