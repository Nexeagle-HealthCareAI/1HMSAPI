namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetImageUrlResponseModel
    {
        public string? Url { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}