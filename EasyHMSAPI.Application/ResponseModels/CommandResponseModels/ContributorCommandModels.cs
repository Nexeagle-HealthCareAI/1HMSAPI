using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ContributorOtpSendResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // Seconds the caller must wait before asking for another code (when throttled).
        public int? RetryAfterSeconds { get; set; }
        // Same meaning as Success, named as the Doctor Dekho pages expect.
        public bool Sent => Success;
    }

    [ExcludeFromCodeCoverage]
    public class ContributorOtpVerifyResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // Handed to the Doctor Dekho server, which keeps it in an HttpOnly cookie. Never shown to page scripts.
        public string? SessionToken { get; set; }
        public DateTime? SessionExpiresAt { get; set; }
        public bool NeedsRegistration { get; set; }
        // Same meaning as Success, named as the Doctor Dekho pages expect.
        public bool SignedIn => Success;
    }

    [ExcludeFromCodeCoverage]
    public class ContributorAdminResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public ContributorAdminInfo? Contributor { get; set; }
        // The invitation link, returned ONLY when it was just created (the raw token is never stored), so the CMS can copy it
        // when WhatsApp delivery is off or failed.
        public string? Link { get; set; }
        // True when WhatsApp accepted the invitation message.
        public bool LinkDelivered { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ContributorSimpleResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
    }
}
