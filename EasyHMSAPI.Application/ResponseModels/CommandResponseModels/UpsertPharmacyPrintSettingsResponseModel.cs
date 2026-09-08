using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPharmacyPrintSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
