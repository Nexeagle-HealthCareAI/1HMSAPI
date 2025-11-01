using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(Guid entityId, IFormFile? file, string containerName, CancellationToken cancellationToken);
        Task<object?> GetUrlAsync(Guid entityId, string containerName, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(string entityId, string containerName, CancellationToken cancellationToken);
    }
}
