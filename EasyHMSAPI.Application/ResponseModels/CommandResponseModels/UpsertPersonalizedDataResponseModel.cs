namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UpsertPersonalizedDataResponseModel
    {
        public string Message { get; set; } = "Success";
        public Guid PersonalId { get; set; }
    }
}
