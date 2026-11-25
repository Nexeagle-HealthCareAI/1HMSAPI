namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// Thin chat-completion client for clinical AI features (discharge drafting, dictation sorting,
    /// section rewrites). Prompts and parsing live in DischargeAiRules; this only talks to the
    /// provider resolved by AiProviderConfig (Groq by default).
    /// </summary>
    public interface IClinicalAiClient
    {
        bool IsConfigured { get; }
        string NotConfiguredMessage { get; }

        /// <summary>Returns the model's raw JSON object text (JSON mode).</summary>
        Task<string> CompleteJsonAsync(string systemPrompt, string userContent, CancellationToken cancellationToken);

        /// <summary>Returns plain text.</summary>
        Task<string> CompleteTextAsync(string systemPrompt, string userContent, CancellationToken cancellationToken);
    }
}
