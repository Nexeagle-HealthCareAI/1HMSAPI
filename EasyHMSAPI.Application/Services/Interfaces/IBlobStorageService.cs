using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(string entityId, IFormFile? file, string containerName, CancellationToken cancellationToken);
        Task<object?> GetUrlAsync(Guid entityId, string containerName, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string entityId, string containerName, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a currently-valid URL for the object identified by <paramref name="objectKeyPrefix"/>.
        /// Azure (long-lived SAS): returns <paramref name="storedUrl"/> unchanged — no extra work.
        /// S3/MinIO (presigned URLs cap at 7 days): re-signs a fresh URL from the stored object key,
        /// so links persisted in the DB never go stale. Falls back to <paramref name="storedUrl"/> if
        /// the object can't be resolved, so it never makes a read worse than it is today.
        /// </summary>
        Task<string?> RefreshUrlAsync(string containerName, string objectKeyPrefix, string? storedUrl, CancellationToken cancellationToken);
    }
}
