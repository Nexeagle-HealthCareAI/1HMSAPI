using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Fhir
{
    /// <summary>Reads an uploaded document back from storage so it can travel as a Health Document record.</summary>
    public interface IAbdmDocumentFiles
    {
        /// <returns>The file, or null when it cannot be read, is too large, or is not a PDF or an image.</returns>
        Task<FhirFile?> ReadAsync(string container, string keyPrefix, string? storedUrl, string title, string? fileName, DateTime? uploadedAt, CancellationToken cancellationToken);
    }

    public sealed class AbdmDocumentFiles : IAbdmDocumentFiles
    {
        /// <summary>An encrypted push page holds about 4 MB; one document never takes more than that.</summary>
        public const int MaxBytes = 3_000_000;

        private readonly IBlobStorageService _storage;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<AbdmDocumentFiles> _logger;

        public AbdmDocumentFiles(IBlobStorageService storage, IHttpClientFactory http, ILogger<AbdmDocumentFiles> logger)
        {
            _storage = storage;
            _http = http;
            _logger = logger;
        }

        public async Task<FhirFile?> ReadAsync(string container, string keyPrefix, string? storedUrl, string title, string? fileName, DateTime? uploadedAt, CancellationToken cancellationToken)
        {
            var contentType = ContentTypeFor(fileName ?? title);
            if (contentType == null)
                return null;
            try
            {
                var url = await _storage.RefreshUrlAsync(container, keyPrefix, storedUrl, cancellationToken);
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    return null;
                using var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
                    return null;
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (data.Length == 0 || data.Length > MaxBytes)
                    return null;
                return new FhirFile(string.IsNullOrWhiteSpace(title) ? "Document" : title.Trim(), contentType, data, uploadedAt);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning("ABDM: an uploaded document could not be read for sharing ({Error}).", ex.GetType().Name);
                return null;
            }
        }

        /// <summary>The PHR app shows PDFs and images; other uploads (Word, DICOM...) are not shared.</summary>
        public static string? ContentTypeFor(string? name)
        {
            var extension = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => null
            };
        }
    }
}
