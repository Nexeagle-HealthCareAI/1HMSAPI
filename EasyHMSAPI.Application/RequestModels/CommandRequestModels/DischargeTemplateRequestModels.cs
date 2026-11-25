using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Reusable discharge summary templates, shared by the hospital.

    [ExcludeFromCodeCoverage]
    public class GetDischargeTemplatesRequestModel : IRequest<DischargeTemplatesResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveDischargeTemplateRequestModel : IRequest<SaveDischargeTemplateResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>Set to change an existing template; leave empty to create one.</summary>
        public Guid? DischargeTemplateId { get; set; }
        public string? Name { get; set; }
        /// <summary>The chosen sections and medicines, as the discharge screen serialises them.</summary>
        public string? ContentJson { get; set; }
        /// <summary>When creating: overwrite the template that already has this name instead of refusing.</summary>
        public bool ReplaceExisting { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DeleteDischargeTemplateRequestModel : IRequest<SaveDischargeTemplateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DischargeTemplateId { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MarkDischargeTemplateUsedRequestModel : IRequest<SaveDischargeTemplateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DischargeTemplateId { get; set; }
    }
}
