using EasyHMSAPI.Application.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Salted, adaptive password hashing (PBKDF2-HMAC-SHA256) with transparent migration from the
    /// legacy format (unsalted SHA-256 hex, optionally passed through <see cref="IMaskingService"/>).
    ///
    /// Stored format: <c>PBKDF2-SHA256$&lt;iterations&gt;$&lt;salt b64&gt;$&lt;hash b64&gt;</c> (~90 chars; the
    /// column is NVARCHAR(256)). The iteration count travels with the hash, so it can be raised later
    /// and old hashes are upgraded on the next successful login (see <see cref="NeedsRehash"/>).
    /// Legacy hashes keep verifying until each user next signs in; nothing needs a bulk migration.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        /// <summary>OWASP-recommended work factor for PBKDF2-HMAC-SHA256.</summary>
        public const int CurrentIterations = 600_000;

        public static string Hash(string password, int iterations = CurrentIterations)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
            return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool IsModern(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

        /// <summary>True for any legacy hash, or a modern one below the current work factor.</summary>
        public static bool NeedsRehash(string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (!IsModern(stored)) return true;
            return TryParse(stored, out var iterations, out _, out _) && iterations < CurrentIterations;
        }

        /// <summary>Legacy format: lowercase hex of an unsalted SHA-256. Kept only to verify/migrate old rows.</summary>
        public static string LegacyHash(string password) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

        public static bool Verify(string? password, string? stored, IMaskingService masking)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;

            if (IsModern(stored))
            {
                if (!TryParse(stored, out var iterations, out var salt, out var expected)) return false;
                var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            var legacy = LegacyHash(password);
            var candidate = masking.IsMaskingEnabled() ? masking.Mask(legacy) : legacy;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(stored));
        }

        private static bool TryParse(string stored, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0; salt = Array.Empty<byte>(); hash = Array.Empty<byte>();
            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out iterations) || iterations <= 0) return false;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                hash = Convert.FromBase64String(parts[3]);
                return salt.Length > 0 && hash.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
