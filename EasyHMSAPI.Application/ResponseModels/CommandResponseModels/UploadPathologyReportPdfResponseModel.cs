namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UploadPathologyReportPdfResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Url { get; set; }
        public string? Sha256 { get; set; }
    }
}
