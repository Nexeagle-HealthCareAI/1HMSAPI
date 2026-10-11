using EasyHMSAPI.Application.Services.Models;
using Microsoft.AspNetCore.Http;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// Checks and stores an image for a Health Wiki article, for a contributor on the Doctor Dekho pages and for the CMS alike:
    /// JPG, PNG or WebP by what the file really is, at most 2 MB, a random name, and a record of who sent it.
    /// </summary>
    public interface IHealthWikiImageService
    {
        Task<HealthWikiImageResult> UploadAsync(IFormFile? file, Guid? contributorId, string? uploadedByName, CancellationToken cancellationToken);
    }
}
