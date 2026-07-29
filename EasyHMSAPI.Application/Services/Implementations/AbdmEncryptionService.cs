using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Fetches ABDM's RSA public certificate (PEM, from the "CertUrl" configured under "Abdm") and
    /// uses it to RSA-OAEP-SHA1-encrypt PII before it's sent to any ABHA V3 API, per the ABDM
    /// integrator guide. The certificate is cached and only refetched on expiry or a decrypt-side
    /// rejection (ABDM rotates it without notice).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbdmEncryptionService : IAbdmEncryptionService
    {
        private const string CacheKey = "Abdm:PublicCertPem";
        private static readonly SemaphoreSlim FetchLock = new(1, 1);

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _certUrl;

        public AbdmEncryptionService(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _certUrl = configuration["Abdm:CertUrl"] ?? "https://healthidsbx.abdm.gov.in/api/v1/auth/cert";
        }

        public async Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken)
        {
            var pem = await GetPublicCertPemAsync(forceRefresh: false, cancellationToken);
            try
            {
                return Encrypt(plainText, pem);
            }
            catch (CryptographicException)
            {
                // Certificate rotated without notice — refetch once and retry.
                pem = await GetPublicCertPemAsync(forceRefresh: true, cancellationToken);
                return Encrypt(plainText, pem);
            }
        }

        private static string Encrypt(string plainText, string pem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var cipherBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.OaepSHA1);
            return Convert.ToBase64String(cipherBytes);
        }

        private async Task<string> GetPublicCertPemAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh && _cache.TryGetValue(CacheKey, out string? cachedPem) && !string.IsNullOrWhiteSpace(cachedPem))
                return cachedPem;

            await FetchLock.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && _cache.TryGetValue(CacheKey, out string? pemAfterLock) && !string.IsNullOrWhiteSpace(pemAfterLock))
                    return pemAfterLock;

                using var response = await _httpClient.GetAsync(_certUrl, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to fetch ABDM public certificate ({(int)response.StatusCode}).");

                var pem = ExtractPem(body);
                if (string.IsNullOrWhiteSpace(pem))
                    throw new InvalidOperationException("ABDM public certificate response did not contain a PEM key.");

                _cache.Set(CacheKey, pem, TimeSpan.FromHours(6));
                return pem;
            }
            finally
            {
                FetchLock.Release();
            }
        }

        /// <summary>The cert endpoint returns either a raw PEM string body or a JSON object with a
        /// "publicKey"/"certificate" field — handle both.</summary>
        private static string ExtractPem(string body)
        {
            var trimmed = body.Trim();
            if (trimmed.Contains("-----BEGIN", StringComparison.Ordinal) && !trimmed.StartsWith('{'))
                return trimmed;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("publicKey", out var pk) && pk.ValueKind == JsonValueKind.String)
                    return pk.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("certificate", out var cert) && cert.ValueKind == JsonValueKind.String)
                    return cert.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                // Not JSON — fall through and treat the whole trimmed body as the PEM.
            }

            return trimmed;
        }
    }
}
