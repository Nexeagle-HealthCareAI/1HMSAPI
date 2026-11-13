using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EasyHMSAPI.Application.Exceptions;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// The Health Wiki image bucket (HealthWiki:ImageBucket) on the same S3-compatible store as the rest of EasyHMS (Storage:S3:ServiceUrl).
    /// It prefers its own access key (HealthWiki:ImageAccessKey / ImageSecretKey), which should be allowed on this bucket only, so a fault
    /// here can never reach patient files; without them it falls back to the shared Storage:S3 key. Objects get a random key, are cached for a year (the key changes when the image does), and carry no ACL: the bucket
    /// policy lets anyone read a single file, and nothing else.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class HealthWikiImageStore : IHealthWikiImageStore
    {
        private readonly IAmazonS3? _client;
        private readonly string _bucket;
        private readonly string _publicBaseUrl;

        public HealthWikiImageStore(IConfiguration configuration)
        {
            var serviceUrl = configuration["Storage:S3:ServiceUrl"] ?? string.Empty;
            // The Health Wiki's own key when both halves are set, otherwise the shared one.
            var ownKey = Clean(configuration["HealthWiki:ImageAccessKey"]);
            var ownSecret = Clean(configuration["HealthWiki:ImageSecretKey"]);
            var useOwn = ownKey != null && ownSecret != null;
            var accessKey = useOwn ? ownKey! : configuration["Storage:S3:AccessKey"] ?? string.Empty;
            var secretKey = useOwn ? ownSecret! : configuration["Storage:S3:SecretKey"] ?? string.Empty;
            var region = configuration["Storage:S3:Region"] ?? "us-east-1";
            var forcePathStyle = !bool.TryParse(configuration["Storage:S3:ForcePathStyle"], out var fps) || fps;
            var sharedBucket = configuration["Storage:S3:Bucket"] ?? string.Empty;
            _bucket = Clean(configuration["HealthWiki:ImageBucket"]) ?? string.Empty;

            // The placeholder text in appsettings.json must never count as a real value.
            if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey) || _bucket.Length == 0)
            {
                _publicBaseUrl = string.Empty;
                return;
            }

            // E2E gives a per-bucket address (https://nexeagle-dev.<region>-objectstore.e2enetworks.net). Take the base host off it, so the
            // Health Wiki bucket can be addressed the same way: https://<its bucket>.<base host>.
            var baseUrl = serviceUrl;
            try
            {
                var uri = new Uri(serviceUrl);
                if (sharedBucket.Length > 0 && uri.Host.StartsWith(sharedBucket + ".", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = $"{uri.Scheme}://{uri.Host[(sharedBucket.Length + 1)..]}";
                    forcePathStyle = false;
                }
                var host = new Uri(baseUrl).Host;
                var explicitBase = Clean(configuration["HealthWiki:ImageBaseUrl"]);
                _publicBaseUrl = explicitBase ?? (forcePathStyle ? $"{baseUrl.TrimEnd('/')}/{_bucket}" : $"{new Uri(baseUrl).Scheme}://{_bucket}.{host}");
            }
            catch (UriFormatException)
            {
                _publicBaseUrl = string.Empty;
                return;
            }

            _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey),
                new AmazonS3Config { ServiceURL = baseUrl, ForcePathStyle = forcePathStyle, AuthenticationRegion = region });
        }

        public bool IsConfigured => _client != null && _publicBaseUrl.Length > 0;

        public async Task<string> PutAsync(string key, Stream content, string contentType, long length, CancellationToken cancellationToken)
        {
            if (!IsConfigured) throw new InvalidOperationException("The Health Wiki image bucket is not configured (HealthWiki:ImageBucket and Storage:S3:*).");
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            };
            request.Headers.ContentLength = length;
            request.Headers.CacheControl = "public, max-age=31536000, immutable";
            try
            {
                await _client!.PutObjectAsync(request, cancellationToken);
            }
            catch (AmazonServiceException ex)
            {
                throw new HealthWikiImageStoreException($"{(int)ex.StatusCode} {ex.ErrorCode}", ex);
            }
            return $"{_publicBaseUrl.TrimEnd('/')}/{key}";
        }

        private static string? Clean(string? value)
        {
            var v = value?.Trim();
            return string.IsNullOrEmpty(v) || v.StartsWith("<") ? null : v;
        }
    }
}
