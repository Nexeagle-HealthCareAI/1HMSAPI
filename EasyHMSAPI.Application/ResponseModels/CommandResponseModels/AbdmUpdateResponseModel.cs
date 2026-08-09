using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmUpdateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
    }
}
