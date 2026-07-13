using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Shared between PublicApiClient key issuance (hashes the raw key before storing it) and
    /// PublicApiKeyFilter (hashes an incoming X-Api-Key header the same way to look it up) — the
    /// raw key itself is never persisted, only its hash, same principle as a GitHub PAT.
    /// </summary>
    public static class ApiKeyHasher
    {
        public static string Hash(string rawKey)
        {
            var bytes = Encoding.UTF8.GetBytes(rawKey);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        public static string GenerateRawKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return "nxk_" + Convert.ToBase64String(bytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");
        }
    }
}
