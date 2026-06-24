using EasyHMSAPI.Application.Services.Interfaces;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// S3-compatible (MinIO) object storage. Drop-in replacement for <see cref="BlobStorageService"/>
    /// selected via config (Storage:Provider = "S3").
    ///
    /// LAYOUT: a single shared bucket per environment (Storage:S3:Bucket, e.g. "nexeagle-dev" /
    /// "nexeagle-prod"). The logical container becomes a CATEGORY FOLDER (key prefix) namespaced by
    /// a product prefix (Storage:S3:Prefix, e.g. "1HMS"):
    ///     nexeagle-dev/1HMS_Prescription/{blobName}      (templates, attachments, visit summaries)
    ///     nexeagle-dev/1HMS_ProfilePicture/{blobName}
    ///     nexeagle-dev/1HMS_Invoice/{blobName}
    /// The bucket is pre-created out of band — this service never creates buckets (folders are
    /// implicit in the object key).
    ///
    /// Blob naming and the pipe-delimited attachment return value match <see cref="BlobStorageService"/>
    /// so call sites are unchanged. NOTE: S3/MinIO presigned URLs cannot exceed 7 days (SigV4); read
    /// paths call <see cref="RefreshUrlAsync"/> to re-sign from the persisted key on every read.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class S3StorageService : IBlobStorageService
    {
        private readonly IAmazonS3 _client;
        private readonly string _bucket;
        private readonly string _prefix;
        private readonly TimeSpan _urlExpiry;
        private readonly string _prescriptionAssestsContainer;
        private readonly string _prescriptionAttachmentsContainer;

        public S3StorageService(IConfiguration configuration)
        {
            var serviceUrl = configuration["Storage:S3:ServiceUrl"] ?? string.Empty;
            var accessKey = configuration["Storage:S3:AccessKey"] ?? string.Empty;
            var secretKey = configuration["Storage:S3:SecretKey"] ?? string.Empty;
            var region = configuration["Storage:S3:Region"] ?? "us-east-1";
            var forcePathStyle = !bool.TryParse(configuration["Storage:S3:ForcePathStyle"], out var fps) || fps; // MinIO needs path-style; default true
            _bucket = configuration["Storage:S3:Bucket"] ?? string.Empty;
            _prefix = (configuration["Storage:S3:Prefix"] ?? "1HMS").Trim();
            _urlExpiry = TimeSpan.FromHours(double.TryParse(configuration["Storage:S3:UrlExpiryHours"], out var h) && h > 0 ? h : 24);

            // Match BlobStorageService's container fields (used only for upload/get branching).
            _prescriptionAssestsContainer = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _prescriptionAttachmentsContainer = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = forcePathStyle,
                AuthenticationRegion = region,
            };
            _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        }

        public async Task<string> UploadAsync(string entityId, IFormFile? file, string containerName, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or missing");

            var folder = FolderFor(containerName);

            if (containerName == _prescriptionAttachmentsContainer)
            {
                var extension = Path.GetExtension(file.FileName);
                var blobName = $"{entityId}_{Guid.NewGuid()}_{SanitizeToken(containerName)}{extension}";
                blobName = string.Concat(blobName.Split(Path.GetInvalidFileNameChars()));

                await PutObjectAsync($"{folder}/{blobName}", file, cancellationToken);

                // Return both the (relative) object name and the presigned URL, pipe-separated (matches Azure).
                var url = await GeneratePresignedUrlAsync($"{folder}/{blobName}", cancellationToken);
                return $"{blobName}|{url}";
            }
            else
            {
                var baseName = $"{entityId}_{containerName}";

                // Remove any prior object for this entity (single logical file per entity).
                var existing = await _client.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = _bucket, Prefix = $"{folder}/{baseName}" }, cancellationToken);
                foreach (var obj in existing.S3Objects ?? new List<S3Object>())
                {
                    await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = obj.Key }, cancellationToken);
                    break;
                }

                var extension = Path.GetExtension(file.FileName);
                var blobName = $"{baseName}{extension}";
                blobName = string.Concat(blobName.Split(Path.GetInvalidFileNameChars()));

                await PutObjectAsync($"{folder}/{blobName}", file, cancellationToken);
                return await GeneratePresignedUrlAsync($"{folder}/{blobName}", cancellationToken);
            }
        }

        public async Task<object?> GetUrlAsync(Guid entityId, string containerName, CancellationToken cancellationToken)
        {
            var folder = FolderFor(containerName);

            if (containerName == _prescriptionAssestsContainer)
            {
                var results = new List<object>();
                var listed = await _client.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = _bucket, Prefix = $"{folder}/{entityId}_" }, cancellationToken);
                foreach (var obj in listed.S3Objects ?? new List<S3Object>())
                {
                    results.Add(new
                    {
                        Id = obj.Key,
                        Url = await GeneratePresignedUrlAsync(obj.Key, cancellationToken)
                    });
                }
                return results;
            }
            else
            {
                var prefix = $"{folder}/{entityId}_{containerName}";
                var listed = await _client.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix }, cancellationToken);
                var match = (listed.S3Objects ?? new List<S3Object>()).FirstOrDefault();
                if (match != null)
                    return await GeneratePresignedUrlAsync(match.Key, cancellationToken);
                return null;
            }
        }

        public async Task<bool> DeleteAsync(string entityId, string containerName, CancellationToken cancellationToken)
        {
            var folder = FolderFor(containerName);
            var relPrefix = containerName == _prescriptionAttachmentsContainer ? $"{entityId}_" : $"{entityId}_{containerName}";

            var listed = await _client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = _bucket, Prefix = $"{folder}/{relPrefix}" }, cancellationToken);
            var match = (listed.S3Objects ?? new List<S3Object>()).FirstOrDefault();
            if (match == null) return false;

            await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = match.Key }, cancellationToken);
            return true;
        }

        public async Task<string?> RefreshUrlAsync(string containerName, string objectKeyPrefix, string? storedUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(objectKeyPrefix)) return storedUrl;
            try
            {
                var prefix = $"{FolderFor(containerName)}/{objectKeyPrefix}";
                var listed = await _client.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix, MaxKeys = 1 }, cancellationToken);
                var key = (listed.S3Objects ?? new List<S3Object>()).FirstOrDefault()?.Key;
                return key != null ? await GeneratePresignedUrlAsync(key, cancellationToken) : storedUrl;
            }
            catch
            {
                // Never make a read worse than it is today — fall back to whatever is persisted.
                return storedUrl;
            }
        }

        // ----------------------------------------------------------------------------------------
        private async Task PutObjectAsync(string key, IFormFile file, CancellationToken cancellationToken)
        {
            await using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true, // MinIO over HTTP: avoids streaming-signature mismatches
            };
            request.Headers.ContentLength = file.Length;
            await _client.PutObjectAsync(request, cancellationToken);
        }

        private async Task<string> GeneratePresignedUrlAsync(string key, CancellationToken cancellationToken)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(_urlExpiry),
            };
            return await _client.GetPreSignedURLAsync(request);
        }

        // Maps a logical container to its "{prefix}_{Category}" folder, e.g. "1HMS_Prescription".
        private string FolderFor(string containerName) => $"{_prefix}_{CategoryFor(containerName)}";

        private static string CategoryFor(string containerName)
        {
            var c = (containerName ?? string.Empty).ToLowerInvariant();
            if (c.Contains("prescription")) return "Prescription";   // templates, attachments, visit summaries
            if (c.Contains("profile")) return "ProfilePicture";
            if (c.Contains("invoice")) return "Invoice";
            return string.IsNullOrWhiteSpace(c) ? "Misc" : char.ToUpperInvariant(c[0]) + c.Substring(1);
        }

        private static string SanitizeToken(string containerName)
            => containerName.ToLowerInvariant().Trim().Replace("_", "-");
    }
}
