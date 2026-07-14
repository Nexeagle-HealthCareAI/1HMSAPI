using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ActivateRapidResponseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ActivationId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateRapidResponseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
