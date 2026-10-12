namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>The session a hand-off code was swapped for. The token goes to the Doctor Dekho server only.</summary>
    public sealed class HealthWikiHandoffSession
    {
        public string Token { get; init; } = null!;
        public DateTime ExpiresAt { get; init; }
    }
}
