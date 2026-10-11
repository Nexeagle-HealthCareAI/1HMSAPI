using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>What a doctor's review decision did, as the review service reports it to the handlers that called it.</summary>
    [ExcludeFromCodeCoverage]
    public class HealthArticleReviewResult
    {
        public const string OutcomePublished = "PUBLISHED";
        public const string OutcomeAwaitingVerification = "APPROVED_AWAITING_VERIFICATION";
        public const string OutcomeSentBack = "SENT_BACK";

        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // PUBLISHED (live now, or the edit is now live) | APPROVED_AWAITING_VERIFICATION | SENT_BACK
        public string? Outcome { get; set; }
        public HealthArticle? Article { get; set; }
        public string? ReviewerName { get; set; }

        public static HealthArticleReviewResult Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    /// <summary>Result of a "Helpful" tap or a page view.</summary>
    [ExcludeFromCodeCoverage]
    public class HealthArticleReactionResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // False when it was already counted (the visitor liked it before, or this visitor's view today).
        public bool Changed { get; set; }
        public long Views { get; set; }
        public long Likes { get; set; }

        public static HealthArticleReactionResult Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
