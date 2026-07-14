using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDischargeSummaryPdfResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // A freshly re-signed, currently-valid URL for the stored PDF -- minted on every request
        // (never persisted), since presigned S3 URLs expire.
        public string? RedirectUrl { get; set; }
    }
}
