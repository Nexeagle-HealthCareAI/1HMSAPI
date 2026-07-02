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

        /// <summary>Writes a concise professional prose narrative (plain text, not JSON) from
        /// already-structured clinical source material — e.g. a discharge summary's "Course in
        /// Hospital" section composed from round notes/procedures/medications. Text-in-text-out,
        /// no audio/transcription involved. <paramref name="sectionHint"/> names the target
        /// document section so the prompt can tailor tone/length (e.g. "Course in Hospital").</summary>
        Task<string> NarrateAsync(string sourceMaterial, string sectionHint, CancellationToken cancellationToken);
    }
}
