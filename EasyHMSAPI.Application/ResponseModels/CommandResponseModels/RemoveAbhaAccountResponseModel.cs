using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RemoveAbhaAccountResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
