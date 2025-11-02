using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPersonalizedDataResponseModel
    {
        public string Message { get; set; } = "Success";
        public Guid PersonalId { get; set; }
    }
}
