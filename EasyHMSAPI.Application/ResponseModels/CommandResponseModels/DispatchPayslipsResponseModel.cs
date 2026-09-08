namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class DispatchPayslipsResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DispatchedCount { get; set; }
    }
}
