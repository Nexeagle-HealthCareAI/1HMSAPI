using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IVoiceRxService
    {
        /// <summary>Transcribes an audio clip to text. When <paramref name="diarize"/> is true and a
        /// diarizing provider (Deepgram) is configured, the transcript is labelled by speaker
        /// ("Speaker 0:", "Speaker 1:"); otherwise OpenAI Whisper is used.</summary>
        Task<string> TranscribeAsync(byte[] audio, string fileName, string? language, bool diarize, CancellationToken cancellationToken);

        /// <summary>Structures a transcript into prescription fields (LLM, strict JSON). Mode is
        /// "dictation" (doctor speaking the Rx) or "ambient" (a recorded doctor-patient consultation).</summary>
        Task<VoiceRxFieldsModel> StructureAsync(string transcript, string? mode, CancellationToken cancellationToken);
    }
}
