using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePublicApiClientResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ApiClientId { get; set; }
        // Raw key — shown once, never retrievable again after this response.
        public string? ApiKey { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RevokePublicApiClientResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
