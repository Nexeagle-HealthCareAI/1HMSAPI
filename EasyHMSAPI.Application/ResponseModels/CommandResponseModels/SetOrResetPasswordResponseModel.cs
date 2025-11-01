namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class SetOrResetPasswordResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
