using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Free-text fallback contract: when IsFreeText=true, FreeTextNote carries the whole handover
    // and the 4 SBAR fields are ignored/nulled regardless of what the client sent — a nurse is
    // never forced into the structured fields. When IsFreeText=false, only Situation is required.
    [ExcludeFromCodeCoverage]
    public class CreateShiftHandoverNoteRequestModel : IRequest<CreateShiftHandoverNoteResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string ShiftCode { get; set; } = null!;   // MORNING / EVENING / NIGHT
        public string OutgoingNurseName { get; set; } = null!;
        public Guid? OutgoingNurseUserId { get; set; }
        public string? IncomingNurseName { get; set; }
        public Guid? IncomingNurseUserId { get; set; }

        public bool IsFreeText { get; set; }
        public string? FreeTextNote { get; set; }

        public string? Situation { get; set; }
        public string? Background { get; set; }
        public string? Assessment { get; set; }
        public string? Recommendation { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AcknowledgeShiftHandoverRequestModel : IRequest<AcknowledgeShiftHandoverResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid ShiftHandoverNoteId { get; set; }
        public string IncomingNurseName { get; set; } = null!;
    }
}
