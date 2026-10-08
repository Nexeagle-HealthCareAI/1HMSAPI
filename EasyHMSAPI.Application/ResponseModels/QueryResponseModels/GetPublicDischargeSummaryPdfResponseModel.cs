using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDischargeSummaryPdfResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // The link was valid once but has passed its expiry (the controller answers 410 Gone).
        public bool Expired { get; set; }
        // A freshly re-signed, currently-valid URL for the stored PDF -- minted on every request
        // (never persisted), since presigned S3 URLs expire.
        public string? RedirectUrl { get; set; }
    }
}
