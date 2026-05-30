using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class RaiseAlertRequestModel : IRequest<RaiseAlertResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AlertCode { get; set; } = null!;
        public string? Severity { get; set; }
        public string Title { get; set; } = null!;
        public string? Body { get; set; }

        public string? PatientId { get; set; }
        public Guid? AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }

        public List<string>? AudienceRoles { get; set; }
        public Guid? AudienceUserId { get; set; }
        public string? AudienceWardCode { get; set; }

        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        public bool? DispatchSms { get; set; }
        public bool? DispatchWhatsApp { get; set; }
        public bool? DispatchInApp { get; set; }
        public string? DispatchToPhone { get; set; }
        public string? PayloadJson { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
