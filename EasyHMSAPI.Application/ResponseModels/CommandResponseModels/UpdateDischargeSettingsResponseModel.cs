using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDischargeSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid DischargeSettingId { get; set; }
    }
}
