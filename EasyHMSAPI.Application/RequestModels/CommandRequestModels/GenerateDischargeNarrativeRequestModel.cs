using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Deliberately takes only IDs, not raw text — the handler re-derives the same source material
    // GetDischargeSummaryDraftHandler composes (via DischargeSummaryComposer), rather than trusting
    // client-relayed clinical text into a prompt. Text-in-text-out, no audio/transcription.
    [ExcludeFromCodeCoverage]
    public class GenerateDischargeNarrativeRequestModel : IRequest<GenerateDischargeNarrativeResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }

        public Guid AdmissionId { get; set; }
    }
}
