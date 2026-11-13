using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Privacy rules applied when website tracking data is written (decided 2026-10-19):
    ///  * IP addresses are truncated before storage (IPv4 to /24, IPv6 to /48). Region lookup still uses
    ///    the full IP, in memory only.
    ///  * Mobile numbers in AnalyticsEvents are stored as a masked copy (98XXXXXX10) plus a keyed hash
    ///    (HMAC-SHA256 with Tracking:MobileHashKey) so the CMS can still group and find a number
    ///    without the number itself being stored. HospitalLeads keep the number: hospitals call back.
    /// NexeagleCMSAPI has an identical copy (CMSAPI.Data/Repositories/TrackingPrivacy.cs) used to
    /// hash a searched number; keep the two in step, and the key identical in both services.
    /// </summary>
    public static class TrackingPrivacy
    {
        public const int MinHashKeyLength = 32;

        public static string? TruncateIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress.Trim(), out var ip))
                return null;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            var bytes = ip.GetAddressBytes();
            var keep = ip.AddressFamily == AddressFamily.InterNetwork ? 3 : 6;   // /24 or /48
            for (var i = keep; i < bytes.Length; i++) bytes[i] = 0;
            return new IPAddress(bytes).ToString();
        }

        /// <summary>Digits only, last 10 digits (drops a +91 / 0 prefix). Null when fewer than 6 digits.</summary>
        public static string? NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return null;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length < 6) return null;
            return digits.Length > 10 ? digits[^10..] : digits;
        }

        /// <summary>"9876543210" -> "98XXXXXX10". Same rule as the CMS display mask.</summary>
        public static string? MaskMobile(string? normalizedMobile)
        {
            if (string.IsNullOrEmpty(normalizedMobile)) return null;
            if (normalizedMobile.Length <= 4) return new string('X', normalizedMobile.Length);
            return normalizedMobile[..2] + new string('X', normalizedMobile.Length - 4) + normalizedMobile[^2..];
        }

        /// <summary>Lower-case hex HMAC-SHA256 of the normalized number, or null without a usable key.</summary>
        public static string? HashMobile(string? normalizedMobile, string? key)
        {
            if (string.IsNullOrEmpty(normalizedMobile) || string.IsNullOrEmpty(key) || key.Length < MinHashKeyLength)
                return null;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedMobile))).ToLowerInvariant();
        }
    }
}
