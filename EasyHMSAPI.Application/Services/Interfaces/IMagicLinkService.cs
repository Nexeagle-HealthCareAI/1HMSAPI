namespace EasyHMSAPI.Application.Services.Interfaces
{
    public class MagicLinkExchangeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public Guid? UserId { get; set; }
        public Guid? HospitalId { get; set; }
        public string? TargetPath { get; set; }
    }

    /// <summary>
    /// One-tap sign-in links for staff notifications. A link is single-use, short-lived and bound to
    /// one user + hospital + landing page; it replaces (never carries) the user's credentials.
    /// </summary>
    public interface IMagicLinkService
    {
        /// <summary>
        /// Issues a link for <paramref name="userId"/> at <paramref name="hospitalId"/> and returns the
        /// full URL to put in a message. <paramref name="targetPath"/> must be an in-app relative path.
        /// </summary>
        Task<string> CreateLinkAsync(Guid userId, Guid hospitalId, string targetPath, string purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// Redeems a link token for a normal session (JWT). Every failure returns the same generic
        /// message so a caller cannot tell "unknown" from "used" from "expired"; the real reason is logged.
        /// </summary>
        Task<MagicLinkExchangeResult> ExchangeAsync(string token, string? clientIp, CancellationToken cancellationToken = default);
    }
}
