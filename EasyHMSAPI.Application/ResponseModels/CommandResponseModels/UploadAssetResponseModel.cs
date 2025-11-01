namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UploadAssetResponseModel
    {
        public bool Success { get; set; }
        public string? AssestUrl { get; set; }
        public string? Message { get; set; }
    }
}