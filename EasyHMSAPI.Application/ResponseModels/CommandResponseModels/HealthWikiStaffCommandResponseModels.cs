using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    // After a hospital doctor's decision: PUBLISHED, APPROVED_AWAITING_VERIFICATION (their registration is still being checked) or SENT_BACK.
    [ExcludeFromCodeCoverage]
    public class ReviewHealthWikiArticleResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public string? Outcome { get; set; }

        public static ReviewHealthWikiArticleResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // After a reader's "Helpful" or page view.
    [ExcludeFromCodeCoverage]
    public class HealthArticleReactionResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // False when it had already been counted.
        public bool Changed { get; set; }
        public long Views { get; set; }
        public long Likes { get; set; }

        public static HealthArticleReactionResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
