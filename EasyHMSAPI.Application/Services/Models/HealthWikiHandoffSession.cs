namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>The session a hand-off code was swapped for. The token goes to the Doctor Dekho server only.</summary>
    public sealed class HealthWikiHandoffSession
    {
        public string Token { get; init; } = null!;
        public DateTime ExpiresAt { get; init; }
    }

    /// <summary>A contributor session opened for a signed-in patient, and whether the person still has to fill in their contributor profile.</summary>
    public sealed class HealthWikiPatientSignIn
    {
        public HealthWikiHandoffSession Session { get; init; } = null!;
        public bool NeedsRegistration { get; init; }
    }
}
