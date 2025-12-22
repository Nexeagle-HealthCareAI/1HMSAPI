using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPersonalizedDataResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "Success";
        public Guid PersonalId { get; set; }
    }
}
