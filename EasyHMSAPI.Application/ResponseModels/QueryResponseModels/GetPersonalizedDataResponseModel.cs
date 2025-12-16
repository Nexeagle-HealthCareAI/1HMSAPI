using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPersonalizedDataResponseModel
    {
        public Guid PersonalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortDesc { get; set; }
        public string? Code { get; set; }
        public string? Synonyms { get; set; }
        public long UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
