using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AlertActionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Status { get; set; }
    }
}
