using EasyHMSAPI.Application.Exceptions;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiImageService : IHealthWikiImageService
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiImageStore _store;
        private readonly ILogger<HealthWikiImageService> _logger;

        public HealthWikiImageService(AppDbContext context, IHealthWikiImageStore store, ILogger<HealthWikiImageService> logger)
        {
            _context = context;
            _store = store;
            _logger = logger;
        }

        public async Task<HealthWikiImageResult> UploadAsync(IFormFile? file, Guid? contributorId, string? uploadedByName, CancellationToken ct)
        {
            if (!_store.IsConfigured) return HealthWikiImageResult.Fail(503, "Image upload is not available on this environment yet.");
            if (file == null || file.Length == 0) return HealthWikiImageResult.Fail(400, "Choose an image to upload.");
            if (file.Length > HealthWikiImageRules.MaxBytes) return HealthWikiImageResult.Fail(400, "The image is larger than 2 MB. Choose a smaller one.");

            if (contributorId.HasValue)
            {
                var since = DateTime.UtcNow.AddHours(-24);
                var today = await _context.HealthArticleImages.CountAsync(i => i.ContributorId == contributorId && i.CreatedAt > since, ct);
                if (today >= HealthWikiImageRules.MaxUploadsPerDay)
                    return HealthWikiImageResult.Fail(429, "You have reached today's limit for image uploads. Try again tomorrow.");
            }

            // What the file really is comes from its first bytes, not from its name or the type the browser says.
            var header = new byte[HealthWikiImageRules.HeaderBytes];
            int read;
            await using (var peek = file.OpenReadStream())
                read = await peek.ReadAsync(header.AsMemory(0, header.Length), ct);
            var type = HealthWikiImageRules.Detect(header.AsSpan(0, read));
            if (type == null) return HealthWikiImageResult.Fail(400, "Only JPG, PNG or WebP images can be uploaded.");

            var key = HealthWikiImageRules.NewKey(type.Value.Extension);
            string url;
            try
            {
                await using var content = file.OpenReadStream();
                url = await _store.PutAsync(key, content, type.Value.ContentType, file.Length, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The Health Wiki image {Key} could not be stored", key);
                var why = ex is HealthWikiImageStoreException store ? store.Code : ex.GetType().Name;
                return HealthWikiImageResult.Fail(502, "The image could not be saved. Please try again.", why);
            }

            // Readers' browsers only load https images, and the database refuses anything else.
            if (!HealthArticleRules.IsHttpsUrl(url))
            {
                _logger.LogError("The Health Wiki image address {Url} is not https; check HealthWiki:ImageBaseUrl / Storage:S3:ServiceUrl", url);
                return HealthWikiImageResult.Fail(502, "The image could not be saved. Please try again.", "address-not-https");
            }

            _context.HealthArticleImages.Add(new HealthArticleImage
            {
                ImageId = Guid.NewGuid(), ContributorId = contributorId, UploadedByName = uploadedByName is { Length: > 200 } n ? n[..200] : uploadedByName,
                Url = url, ContentType = type.Value.ContentType, SizeBytes = (int)file.Length, CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(ct);
            return new HealthWikiImageResult { Success = true, StatusCode = 201, Url = url };
        }
    }
}
