using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Creates a SOAP round note. The 24h-edit-lock -> addendum rule is primarily a frontend
    // affordance (see RoundNoteRules.EditLockWindow / RoundNotePanel.tsx) — this handler just
    // accepts/validates ParentNoteId + AddendumReason when the client supplies them; it never
    // silently merges into an existing note.
    [ExcludeFromCodeCoverage]
    public class CreateRoundNoteRequestModel : IRequest<CreateRoundNoteResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public Guid? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public DateTime? NotedAt { get; set; }

        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? Diagnosis { get; set; }

        public Guid? ParentNoteId { get; set; }
        public string? AddendumReason { get; set; }
    }
}
