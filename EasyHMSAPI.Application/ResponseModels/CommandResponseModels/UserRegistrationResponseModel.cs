namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UserRegistrationResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? UserId { get; set; }
        public string? AccessToken { get; set; }
    }
} 