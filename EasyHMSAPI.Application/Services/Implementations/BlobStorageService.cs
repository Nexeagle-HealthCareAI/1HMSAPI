using EasyHMSAPI.Application.Services.Interfaces;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly string _connectionString;
        private readonly string _accountName;
        private readonly string _accountKey;
        private readonly string _profilePhotosContainer;
        private readonly string _prescriptionAssestsContainer;
        private readonly string _prescriptionAttachmentsContainer;


        public BlobStorageService(IConfiguration configuration)
        {
            _connectionString = configuration["BlobStorage:StorageConnectionString"] ?? string.Empty;
            _accountName = configuration["BlobStorage:AccountName"] ?? string.Empty;
            _accountKey = configuration["BlobStorage:AccountKey"] ?? string.Empty;
            _profilePhotosContainer = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _prescriptionAssestsContainer = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _prescriptionAttachmentsContainer = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }

        public async Task<string> UploadAsync(string entityId, IFormFile? file, string containerName, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or missing");

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var sanitizedContainerName = containerName.ToLowerInvariant().Trim().Replace("_", "-");
            var containerClient = blobServiceClient.GetBlobContainerClient(sanitizedContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            if (sanitizedContainerName == _prescriptionAttachmentsContainer)
            {
                var extension = Path.GetExtension(file.FileName);
                var blobName = $"{entityId}_{Guid.NewGuid()}_{sanitizedContainerName}{extension}";
                blobName = string.Concat(blobName.Split(Path.GetInvalidFileNameChars()));

                var blobClient = containerClient.GetBlobClient(blobName);
                var headers = new BlobHttpHeaders { ContentType = file.ContentType };

                await using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

                return GenerateSasUrl(containerClient, blobName, TimeSpan.FromDays(365));
            }
            else
            {
                var baseBlobName = $"{entityId}_{containerName}";

                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    if (blobItem.Name.StartsWith(baseBlobName, StringComparison.OrdinalIgnoreCase))
                    {
                        var oldBlob = containerClient.GetBlobClient(blobItem.Name);
                        await oldBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                        break;
                    }
                }

                var extension = Path.GetExtension(file.FileName);
                var blobName = $"{baseBlobName}{extension}";
                blobName = string.Concat(blobName.Split(Path.GetInvalidFileNameChars()));

                var blobClient = containerClient.GetBlobClient(blobName);
                var headers = new BlobHttpHeaders { ContentType = file.ContentType };

                await using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

                return GenerateSasUrl(containerClient, blobName, TimeSpan.FromDays(365));
            }
        }

        public async Task<object?> GetUrlAsync(Guid entityId, string containerName, CancellationToken cancellationToken)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (containerName == _prescriptionAssestsContainer)
            {
                var results = new List<object>();

                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    if (blobItem.Name.StartsWith(entityId.ToString() + "_", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new
                        {
                            Id = blobItem.Name,
                            Url = GenerateSasUrl(containerClient, blobItem.Name, TimeSpan.FromDays(365))
                        });
                    }
                }

                return results;
            }
            else
            {
                var blobName = $"{entityId}_{containerName}";
                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    if (blobItem.Name.StartsWith(blobName, StringComparison.OrdinalIgnoreCase))
                    {
                        return GenerateSasUrl(containerClient, blobItem.Name, TimeSpan.FromDays(365));
                    }
                }
                return null;
            }
        }

        public async Task<bool> DeleteAsync(string entityId, string containerName, CancellationToken cancellationToken)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (containerName == _prescriptionAttachmentsContainer)
            {
                var blobClient = containerClient.GetBlobClient(entityId.ToString());
                return await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            else
            {
                var blobName = $"{entityId}_{containerName}";
                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    if (blobItem.Name.StartsWith(blobName, StringComparison.OrdinalIgnoreCase))
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        return await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    }
                }

                return false;
            }
        }

        private string GenerateSasUrl(BlobContainerClient containerClient, string blobName, TimeSpan expiryDuration)
        {
            var blobClient = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerClient.Name,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expiryDuration)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var credential = new StorageSharedKeyCredential(_accountName, _accountKey);
            var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();

            return $"{blobClient.Uri}?{sasToken}";
        }
    }
}
