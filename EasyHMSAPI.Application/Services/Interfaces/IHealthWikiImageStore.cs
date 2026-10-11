namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// Where Health Wiki images are kept: their own bucket, apart from the patient files, with only reading of single files open to
    /// everyone (never listing). Kept separate from IBlobStorageService on purpose, so nothing about patient storage changes.
    /// </summary>
    public interface IHealthWikiImageStore
    {
        /// <summary>False when the bucket or its credentials are not set on this environment.</summary>
        bool IsConfigured { get; }

        /// <summary>Stores the file and returns the address a reader's browser can open.</summary>
        Task<string> PutAsync(string key, Stream content, string contentType, long length, CancellationToken cancellationToken);
    }
}
