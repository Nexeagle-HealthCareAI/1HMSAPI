using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Exceptions
{
    /// <summary>
    /// The image bucket refused or failed a request. Carries only the storage service's own status and error code (for example
    /// "403 AccessDenied" or "404 NoSuchBucket"), never a key or a request body, so it is safe to show to the CMS team.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class HealthWikiImageStoreException : Exception
    {
        public string Code { get; }

        public HealthWikiImageStoreException(string code, Exception inner) : base($"The Health Wiki image store answered {code}.", inner)
        {
            Code = code;
        }
    }
}
