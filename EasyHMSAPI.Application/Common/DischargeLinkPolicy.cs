using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Lifetime rules for the anonymous "view my discharge summary" link (QR code / WhatsApp). The link used
    /// to work forever; it now expires, and a fresh token replaces the old one when it is regenerated, when the
    /// summary is unsigned, or when an expired link is asked for again.
    /// </summary>
    public static class DischargeLinkPolicy
    {
        public const int DefaultValidityDays = 30;

        public static string NewToken() => RandomNumberGenerator.GetHexString(40);

        public static int ValidityDays(IConfiguration? configuration)
        {
            var configured = configuration?["DischargeSummary:PublicLinkValidityDays"];
            return int.TryParse(configured, out var days) && days is > 0 and <= 3650 ? days : DefaultValidityDays;
        }

        public static DateTime ExpiryFrom(DateTime nowUtc, IConfiguration? configuration) => nowUtc.AddDays(ValidityDays(configuration));

        public static bool IsExpired(DateTime? expiresAtUtc, DateTime nowUtc) => expiresAtUtc.HasValue && expiresAtUtc.Value <= nowUtc;
    }
}
