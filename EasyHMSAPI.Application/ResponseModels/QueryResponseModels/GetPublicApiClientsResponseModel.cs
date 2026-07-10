using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicApiClientsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicApiClientSummaryModel> Clients { get; set; } = new();
    }

    // Never carries ApiKeyHash or the raw key — masked/summary view only.
    [ExcludeFromCodeCoverage]
    public class PublicApiClientSummaryModel
    {
        public Guid ApiClientId { get; set; }
        public string? ClientName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
