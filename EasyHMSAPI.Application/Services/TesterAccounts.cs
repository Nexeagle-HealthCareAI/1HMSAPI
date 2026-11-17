using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Reusable test accounts for the staff sign-up and onboarding journey.
    ///
    /// A "tester number" is a mobile listed in <c>StaffAuth:TesterMobiles</c> (comma-separated 10-digit numbers).
    /// It only counts on a Development server: anywhere else every method here answers false, so a setting copied
    /// to production by mistake changes nothing. For a tester number on Development:
    /// <list type="bullet">
    /// <item>the sign-up / login code is handed back to the page instead of going to WhatsApp (OtpSendHandler);</item>
    /// <item>no WhatsApp message of any kind is sent to it (WhatsAppMessagingService), so a made-up number never reaches a stranger;</item>
    /// <item>the account can be wiped and registered again (ResetTesterAccountHandler, POST /internal/tester-accounts/reset).</item>
    /// </list>
    /// </summary>
    public static class TesterAccounts
    {
        public const string MobilesKey = "StaffAuth:TesterMobiles";
        public const string ResetKeyKey = "StaffAuth:TesterResetKey";

        /// <summary>True only on a Development server.</summary>
        public static bool IsDevelopment(IConfiguration configuration) =>
            string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

        /// <summary>The last 10 digits of a number however it is written ("+91 98765 43210", "919876543210", "09876543210").</summary>
        public static string Normalize(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            return digits.Length > 10 ? digits.Substring(digits.Length - 10) : digits;
        }

        /// <summary>The tester numbers of this server (empty unless it is a Development server).</summary>
        public static IReadOnlyCollection<string> Mobiles(IConfiguration configuration)
        {
            if (!IsDevelopment(configuration)) return Array.Empty<string>();
            var raw = configuration[MobilesKey];
            if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("<")) return Array.Empty<string>();
            return raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(m => m.Length == 10)
                .Distinct()
                .ToArray();
        }

        /// <summary>Is this mobile a tester number on this server?</summary>
        public static bool IsTester(IConfiguration configuration, string? mobile)
        {
            var normalized = Normalize(mobile);
            return normalized.Length == 10 && Mobiles(configuration).Contains(normalized);
        }

        /// <summary>The key a reset call must send, or null when resets are switched off (not Development, or no key set).</summary>
        public static string? ResetKey(IConfiguration configuration)
        {
            if (!IsDevelopment(configuration)) return null;
            var key = configuration[ResetKeyKey];
            return string.IsNullOrWhiteSpace(key) || key.StartsWith("<") ? null : key;
        }
    }
}
