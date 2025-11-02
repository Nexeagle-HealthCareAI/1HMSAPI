using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetImageUrlResponseModel
    {
        public string? Url { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}