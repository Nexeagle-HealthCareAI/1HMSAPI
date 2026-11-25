using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Which AI provider the clinical AI features (Voice Rx, discharge drafting/dictation) call.
    /// Groq is the platform default: it is used whenever "Groq:ApiKey" is set. OpenAI stays as a
    /// fallback only for environments that still have just an "OpenAI:ApiKey". Both speak the same
    /// OpenAI-style API (chat/completions, audio/transcriptions), so callers only need the base
    /// URL, key and model names resolved here.
    /// </summary>
    public sealed record AiProviderConfig(string Provider, string ApiKey, string BaseUrl, string ChatModel, string TranscriptionModel)
    {
        public const string GroqDefaultBaseUrl = "https://api.groq.com/openai/v1";
        public const string GroqDefaultChatModel = "openai/gpt-oss-120b";
        public const string GroqDefaultTranscriptionModel = "whisper-large-v3-turbo";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>Message shown when no key is configured, naming the provider that should be set up.</summary>
        public string NotConfiguredMessage => "AI is not configured on this server (missing Groq API key).";

        /// <summary>True for empty values and the "&lt;set-via-env-…&gt;" placeholders committed in appsettings.</summary>
        public static bool IsUnsetKey(string? key) =>
            string.IsNullOrWhiteSpace(key) || key.StartsWith("<", StringComparison.Ordinal) || key == "gsk_dummy";

        public static AiProviderConfig Resolve(IConfiguration configuration)
        {
            var groqKey = configuration["Groq:ApiKey"];
            if (!IsUnsetKey(groqKey))
            {
                return new AiProviderConfig(
                    "Groq",
                    groqKey!.Trim(),
                    TrimUrl(configuration["Groq:BaseUrl"], GroqDefaultBaseUrl),
                    FirstSet(configuration["Groq:ClinicalModel"], configuration["Groq:Model"], GroqDefaultChatModel),
                    FirstSet(configuration["Groq:TranscriptionModel"], GroqDefaultTranscriptionModel));
            }

            var openAiKey = configuration["OpenAI:ApiKey"];
            if (!IsUnsetKey(openAiKey))
            {
                return new AiProviderConfig(
                    "OpenAI",
                    openAiKey!.Trim(),
                    TrimUrl(configuration["OpenAI:BaseUrl"], "https://api.openai.com/v1"),
                    FirstSet(configuration["OpenAI:StructuringModel"], "gpt-4o-mini"),
                    FirstSet(configuration["OpenAI:WhisperModel"], "whisper-1"));
            }

            // Nothing set: report Groq (the intended provider) as unconfigured.
            return new AiProviderConfig("Groq", string.Empty, GroqDefaultBaseUrl, GroqDefaultChatModel, GroqDefaultTranscriptionModel);
        }

        private static string TrimUrl(string? value, string fallback) =>
            (string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()).TrimEnd('/');

        private static string FirstSet(params string?[] values) =>
            values.First(v => !string.IsNullOrWhiteSpace(v))!.Trim();
    }
}
