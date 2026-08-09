using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordVentilatorSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? VentilatorSettingsId { get; set; }
    }
}
