using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Mobile, code and token handling for Health Wiki contributors, kept pure so it can be unit-tested.
    /// Tokens (invite link, session) are 256-bit random, so a plain SHA-256 of them is enough to store.
    /// A 6-digit code is NOT: it has only a million values, so it is stored as an HMAC keyed with a server secret,
    /// which a leaked table alone cannot brute-force.
    /// </summary>
    public static class ContributorSecurity
    {
        public const int OtpLength = 6;
        public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
        public const int MaxOtpAttempts = 5;
        public static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(60);
        public const int MaxOtpPerDay = 5;
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
        public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);

        /// <summary>The 10-digit Indian mobile number, or null. Accepts +91 / 91 / 0 prefixes and separators.</summary>
        public static string? NormalizeMobile(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
            return digits.Length == 10 && digits[0] >= '6' ? digits : null;
        }

        /// <summary>"+91 98••• ••127": enough for the owner to recognise it, never the whole number.</summary>
        public static string MaskMobile(string mobile) =>
            mobile.Length == 10 ? $"+91 {mobile[..2]}••• ••{mobile[^3..]}" : "••••••••••";

        public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        public static string HashOtp(string mobile, string code, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{mobile}:{code}")));
        }

        public static bool OtpMatches(string expectedHash, string mobile, string code, string secret) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHash), Encoding.UTF8.GetBytes(HashOtp(mobile, code, secret)));

        /// <summary>A URL-safe random token (about 43 characters).</summary>
        public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
