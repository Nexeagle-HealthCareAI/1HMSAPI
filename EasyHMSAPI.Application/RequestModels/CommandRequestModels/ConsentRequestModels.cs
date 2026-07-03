using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Creates a new version of a consent template. If an active template already exists for the
    // same (HospitalId, TypeCode, Language), it's flipped to IsActive=false and the new row gets
    // Version = old.Version + 1 — see ConsentTemplateCommandHandlers.
    [ExcludeFromCodeCoverage]
    public class UpsertConsentTemplateRequestModel : IRequest<UpsertConsentTemplateResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public string TypeCode { get; set; } = null!;
        public string? Title { get; set; }
        public string? Language { get; set; }
        public string? BodyHtml { get; set; }
    }

    // Signs one consent record against an active template — the template's content is snapshotted
    // at signing time so later template edits never retroactively change what was signed.
    [ExcludeFromCodeCoverage]
    public class SignConsentRequestModel : IRequest<SignConsentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public Guid ConsentTemplateId { get; set; }
        public string? ProcedureName { get; set; }

        public string SignedByName { get; set; } = null!;
        public string SignerRelation { get; set; } = null!;
        public string? SignerIdType { get; set; }
        public string? SignerIdNumber { get; set; }
        public string? SignatureImageBase64 { get; set; }

        public string? WitnessName { get; set; }
        public string? WitnessRole { get; set; }
    }
}
