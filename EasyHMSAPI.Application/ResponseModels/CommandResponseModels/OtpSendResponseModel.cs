namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class OtpSendResponseModel
    {
        public bool Success { get; set; }
        public bool IsSmsSent { get; set; }
        public bool IsEmailSent { get; set; }
        public string? Message { get; set; }
        public Guid? UserId { get; set; }
    }
} 