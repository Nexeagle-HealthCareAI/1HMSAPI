using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Fetches ABDM's public key (from the "CertUrl" configured under "Abdm") and encrypts PII with
    /// the padding the certificate response names in "encryptionAlgorithm" before it's sent to any
    /// ABHA V3 API. Cached 6h; refetched early if the cached key fails to load. An actual key
    /// ROTATION does not surface here — encrypting with a stale-but-valid public key succeeds
    /// locally and only fails as an ABDM-side decrypt rejection, which the 6h TTL bounds.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbdmEncryptionService : IAbdmEncryptionService
    {
        private const string CacheKey = "Abdm:PublicKey";
        private static readonly SemaphoreSlim FetchLock = new(1, 1);

        private readonly HttpClient _httpClient;
        private readonly IAbdmGatewayService _gatewayService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AbdmEncryptionService> _logger;
        private readonly string _certUrl;
        private readonly string _cmId;

        public AbdmEncryptionService(HttpClient httpClient, IAbdmGatewayService gatewayService, IMemoryCache cache, IConfiguration configuration, IHostEnvironment environment, ILogger<AbdmEncryptionService> logger)
        {
            _httpClient = httpClient;
            _gatewayService = gatewayService;
            _cache = cache;
            _logger = logger;
            var isProd = environment.IsProduction();
            _cmId = isProd ? "abdm" : "sbx";
            // "healthidsbx.abdm.gov.in/api/v1/auth/cert" was a stale pre-ABHA-rebrand (Health ID era)
            // endpoint — it doesn't respond, so every encrypt call hung for the full 100s HttpClient
            // timeout before failing. Confirmed sandbox replacement via NHA's own Postman collection.
            var configuredUrl = configuration[isProd ? "Abdm:CertUrlProd" : "Abdm:CertUrl"];
            _certUrl = !string.IsNullOrWhiteSpace(configuredUrl)
                ? configuredUrl
                : !isProd
                    ? "https://abhasbx.abdm.gov.in/abha/api/v3/profile/public/certificate"
                    : throw new InvalidOperationException("Abdm:CertUrlProd must be configured before ABDM can be used in Production.");
        }

        public async Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken)
        {
            var key = await GetPublicKeyAsync(forceRefresh: false, cancellationToken);
            try
            {
                return Encrypt(plainText, key);
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
            {
                // The cached key failed to load (unexpected format, or CertUrl briefly served a bad
                // response) — refetch once and retry.
                key = await GetPublicKeyAsync(forceRefresh: true, cancellationToken);
                return Encrypt(plainText, key);
            }
        }

        private static string Encrypt(string plainText, AbdmPublicKey key)
        {
            using var rsa = ImportRsaPublicKey(key.Pem);
            var cipherBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), key.Padding);
            return Convert.ToBase64String(cipherBytes);
        }

        /// <summary>The key arrives either as a bare RSA public key ("PUBLIC KEY", which
        /// RSA.ImportFromPem understands directly) or a full X.509 certificate ("CERTIFICATE", which
        /// it does NOT). Route certificates through X509Certificate2 to pull out the RSA key first;
        /// export/re-import so the returned RSA instance's lifetime isn't tied to the certificate's.</summary>
        private static RSA ImportRsaPublicKey(string pem)
        {
            if (pem.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal))
            {
                using var cert = X509Certificate2.CreateFromPem(pem);
                using var certRsa = cert.GetRSAPublicKey()
                    ?? throw new CryptographicException("ABDM certificate does not contain an RSA public key.");
                var rsa = RSA.Create();
                rsa.ImportParameters(certRsa.ExportParameters(false));
                return rsa;
            }

            var bareRsa = RSA.Create();
            bareRsa.ImportFromPem(pem);
            return bareRsa;
        }

        private async Task<AbdmPublicKey> GetPublicKeyAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh && _cache.TryGetValue(CacheKey, out AbdmPublicKey? cached) && cached != null)
                return cached;

            await FetchLock.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && _cache.TryGetValue(CacheKey, out AbdmPublicKey? afterLock) && afterLock != null)
                    return afterLock;

                // Despite the PDF documenting this as a plain unauthenticated GET, the sandbox
                // rejects it with 401 without the same gateway Bearer token every other ABDM call
                // carries — matching AbdmAbhaService.BuildRequestAsync's headers here too.
                var response = await AbdmHttp.SendAsync(_httpClient, _gatewayService, _logger, async () =>
                {
                    var accessToken = await _gatewayService.GetAccessTokenAsync(cancellationToken);
                    var request = new HttpRequestMessage(HttpMethod.Get, _certUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    AbdmHttp.AddStandardHeaders(request, _cmId);
                    return request;
                }, cancellationToken);

                AbdmPublicKey key;
                try
                {
                    key = AbdmCertificate.Parse(response.BodyText);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError("ABDM certificate refused for REQUEST-ID {RequestId}: {Reason}", response.RequestId, ex.Message);
                    throw;
                }

                _logger.LogInformation("ABDM certificate loaded; encryptionAlgorithm {Algorithm}", key.Algorithm);
                _cache.Set(CacheKey, key, TimeSpan.FromHours(6));
                return key;
            }
            finally
            {
                FetchLock.Release();
            }
        }
    }
}
