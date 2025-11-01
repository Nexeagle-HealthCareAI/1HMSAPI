namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UploadImageResponseModel
    {
        public bool Success { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
        public string? Message { get; set; }
    }
}