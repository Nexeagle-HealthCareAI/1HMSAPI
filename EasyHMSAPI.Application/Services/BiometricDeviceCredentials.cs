using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Device access tokens for the JSON push endpoint. A token is 256 random bits, shown to the admin
    /// once at registration and stored only as a SHA-256 hash, so a database leak does not let anyone
    /// impersonate a device. (Tokens are high-entropy random values, not passwords, so a fast hash is
    /// the right tool; there is nothing to brute-force.)
    /// </summary>
    public static class BiometricDeviceCredentials
    {
        public static (string Token, string Hash) Generate()
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return (token, Hash(token));
        }

        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        /// <summary>Constant-time comparison of a presented token against the stored hash.</summary>
        public static bool Matches(string? presentedToken, string storedHash)
        {
            if (string.IsNullOrEmpty(presentedToken) || string.IsNullOrEmpty(storedHash)) return false;
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Hash(presentedToken)),
                Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant()));
        }

        /// <summary>Serial numbers are compared case-insensitively; stored and looked up upper-cased.</summary>
        public static string NormalizeSerial(string? serial) => (serial ?? string.Empty).Trim().ToUpperInvariant();
    }
}
