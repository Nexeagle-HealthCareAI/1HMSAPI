using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Voice Rx: an audio clip of the doctor's dictation, transcribed (Whisper) and structured (LLM)
    /// into prescription fields for the doctor to review.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ParseVoiceRxRequestModel : IRequest<ParseVoiceRxResponseModel>
    {
        public IFormFile? Audio { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? PatientId { get; set; }
        public string? Language { get; set; }   // optional hint: "en", "hi", or null = auto-detect
        public string? Mode { get; set; }       // "dictation" (default) or "ambient" (recorded consultation)

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
