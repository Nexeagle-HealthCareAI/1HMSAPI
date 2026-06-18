using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Voice Rx via OpenAI: Whisper for speech-to-text, then a chat model for structuring the
    /// transcript into prescription fields. Keys/config come from the "OpenAI" appsettings section.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class VoiceRxService : IVoiceRxService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _whisperModel;
        private readonly string _structuringModel;
        private readonly string _deepgramApiKey;
        private readonly string _deepgramBaseUrl;
        private readonly string _deepgramModel;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public VoiceRxService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;
            _baseUrl = (configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');
            _whisperModel = configuration["OpenAI:WhisperModel"] ?? "whisper-1";
            _structuringModel = configuration["OpenAI:StructuringModel"] ?? "gpt-4o-mini";
            _deepgramApiKey = configuration["Deepgram:ApiKey"] ?? string.Empty;
            _deepgramBaseUrl = (configuration["Deepgram:BaseUrl"] ?? "https://api.deepgram.com/v1").TrimEnd('/');
            _deepgramModel = configuration["Deepgram:Model"] ?? "nova-2-medical";
        }

        public Task<string> TranscribeAsync(byte[] audio, string fileName, string? language, bool diarize, CancellationToken cancellationToken)
        {
            // Consultations (diarize) use Deepgram for speaker separation when it's configured;
            // dictations — and any case where Deepgram isn't set up — use OpenAI Whisper.
            if (diarize && !string.IsNullOrWhiteSpace(_deepgramApiKey))
                return TranscribeWithDeepgramAsync(audio, fileName, language, cancellationToken);
            return TranscribeWithWhisperAsync(audio, fileName, language, cancellationToken);
        }

        private async Task<string> TranscribeWithWhisperAsync(byte[] audio, string fileName, string? language, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Voice Rx is not configured (missing OpenAI API key).");

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(audio);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName);
            content.Add(new StringContent(_whisperModel), "model");
            content.Add(new StringContent("json"), "response_format");
            if (!string.IsNullOrWhiteSpace(language))
                content.Add(new StringContent(language), "language");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/transcriptions") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Transcription failed ({(int)response.StatusCode}).");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("text", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
        }

        /// <summary>
        /// Transcribes a recorded consultation with Deepgram, separating speakers so the transcript
        /// reads "Speaker 0: …" / "Speaker 1: …" for the structuring model to attribute correctly.
        /// </summary>
        private async Task<string> TranscribeWithDeepgramAsync(byte[] audio, string fileName, string? language, CancellationToken cancellationToken)
        {
            // nova-2-medical is English-only; fall back to the general model for other languages.
            var model = _deepgramModel;
            var nonEnglish = !string.IsNullOrWhiteSpace(language) && !language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            if (nonEnglish && model.Contains("medical", StringComparison.OrdinalIgnoreCase))
                model = "nova-2";

            var url = $"{_deepgramBaseUrl}/listen?model={model}&diarize=true&punctuate=true&smart_format=true&paragraphs=true";
            if (!string.IsNullOrWhiteSpace(language))
                url += $"&language={Uri.EscapeDataString(language)}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", _deepgramApiKey);
            var byteContent = new ByteArrayContent(audio);
            var mediaType = !string.IsNullOrWhiteSpace(fileName) && fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                ? "audio/ogg" : "audio/webm";
            byteContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            request.Content = byteContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Transcription failed ({(int)response.StatusCode}).");

            return BuildDiarizedTranscript(body);
        }

        /// <summary>Builds a speaker-labelled transcript from a Deepgram diarized response.</summary>
        private static string BuildDiarizedTranscript(string body)
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || !results.TryGetProperty("channels", out var channels)
                || channels.GetArrayLength() == 0)
                return string.Empty;

            var alt = channels[0].GetProperty("alternatives")[0];

            // Preferred: paragraphs already grouped by speaker.
            if (alt.TryGetProperty("paragraphs", out var paragraphs)
                && paragraphs.TryGetProperty("paragraphs", out var pArr)
                && pArr.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var p in pArr.EnumerateArray())
                {
                    var speaker = p.TryGetProperty("speaker", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetInt32() : 0;
                    var text = new StringBuilder();
                    if (p.TryGetProperty("sentences", out var sentences) && sentences.ValueKind == JsonValueKind.Array)
                        foreach (var s in sentences.EnumerateArray())
                            if (s.TryGetProperty("text", out var tx)) text.Append(tx.GetString()).Append(' ');
                    var line = text.ToString().Trim();
                    if (line.Length > 0)
                        sb.Append("Speaker ").Append(speaker).Append(": ").Append(line).Append('\n');
                }
                var grouped = sb.ToString().Trim();
                if (grouped.Length > 0) return grouped;
            }

            // Fallback: stitch words[] together, breaking on speaker change.
            if (alt.TryGetProperty("words", out var words) && words.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                int? current = null;
                foreach (var w in words.EnumerateArray())
                {
                    var speaker = w.TryGetProperty("speaker", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetInt32() : 0;
                    var word = w.TryGetProperty("punctuated_word", out var pw) ? pw.GetString()
                        : (w.TryGetProperty("word", out var ww) ? ww.GetString() : null);
                    if (string.IsNullOrEmpty(word)) continue;
                    if (current != speaker)
                    {
                        if (current != null) sb.Append('\n');
                        sb.Append("Speaker ").Append(speaker).Append(": ");
                        current = speaker;
                    }
                    sb.Append(word).Append(' ');
                }
                var stitched = sb.ToString().Trim();
                if (stitched.Length > 0) return stitched;
            }

            // Last resort: the plain transcript.
            return alt.TryGetProperty("transcript", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
        }

        public async Task<VoiceRxFieldsModel> StructureAsync(string transcript, string? mode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Voice Rx is not configured (missing OpenAI API key).");
            if (string.IsNullOrWhiteSpace(transcript))
                return new VoiceRxFieldsModel();

            const string schema =
                "a JSON object with these keys ONLY: chiefComplaint, history, examination (general examination), systemicExamination, " +
                "diagnosis (all strings); investigations, procedures (arrays of strings); medications (array of objects with name, dose, " +
                "frequency, duration, instructions); advice (array of objects with advice, duration, notes); followUp (object with " +
                "followUpOn and reason). Use empty strings/arrays when something is not mentioned. Do NOT invent clinical content. " +
                "Keep medicine names, doses (e.g. 500mg), frequency (e.g. BD/TDS/once daily) and duration (e.g. 5 days) exactly as stated. Respond with JSON only.";

            var isAmbient = string.Equals(mode, "ambient", StringComparison.OrdinalIgnoreCase);
            var systemPrompt = isAmbient
                ? "You are a clinical scribe listening to a recorded consultation between a doctor and a patient. The transcript may " +
                  "contain both speakers, greetings, small talk, repetition and incomplete sentences, and may be labelled by speaker " +
                  "(e.g. 'Speaker 0:', 'Speaker 1:'). When speaker labels are present, work out which speaker is the doctor (the one " +
                  "examining, ordering investigations and prescribing) and which is the patient (describing symptoms), and attribute " +
                  "information accordingly. Extract ONLY the doctor's clinical decisions and the clinically relevant patient-reported " +
                  "information into " + schema + " Ignore greetings and chit-chat. Only include things that were actually said in the conversation."
                : "You are a clinical scribe for a doctor's prescription. From the doctor's dictation, extract " + schema;

            var payload = new
            {
                model = _structuringModel,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = transcript }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Structuring failed ({(int)response.StatusCode}).");

            using var doc = JsonDocument.Parse(body);
            var contentStr = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(contentStr))
                return new VoiceRxFieldsModel();

            try
            {
                return JsonSerializer.Deserialize<VoiceRxFieldsModel>(contentStr, JsonOptions) ?? new VoiceRxFieldsModel();
            }
            catch
            {
                return new VoiceRxFieldsModel();
            }
        }
    }
}
