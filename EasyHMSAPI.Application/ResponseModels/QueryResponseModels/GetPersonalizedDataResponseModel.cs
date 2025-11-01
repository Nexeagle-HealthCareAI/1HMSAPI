namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetPersonalizedDataResponseModel
    {
        public Guid PersonalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortDesc { get; set; }
        public string? Code { get; set; }
        public string? Synonyms { get; set; }
    }
}
