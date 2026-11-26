using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>A dictation as transcribed, with the phrases the transcriber was unsure of.</summary>
    public sealed record DictationTranscript(string Text, List<string> UnclearPhrases);

    public interface IVoiceRxService
    {
        /// <summary>Transcribes an audio clip to text. When <paramref name="diarize"/> is true and a
        /// diarizing provider (Deepgram) is configured, the transcript is labelled by speaker
        /// ("Speaker 0:", "Speaker 1:"); otherwise Whisper on Groq is used.</summary>
        Task<string> TranscribeAsync(byte[] audio, string fileName, string? language, bool diarize, CancellationToken cancellationToken);

        /// <summary>Transcribes a single-speaker dictation with Whisper, primed with a short
        /// <paramref name="vocabularyHint"/> (e.g. this patient's medicines) so those words are
        /// recognised, and reports the stretches the transcriber itself found hard to hear.</summary>
        Task<DictationTranscript> TranscribeDictationAsync(byte[] audio, string fileName, string? language, string? vocabularyHint, CancellationToken cancellationToken);

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
