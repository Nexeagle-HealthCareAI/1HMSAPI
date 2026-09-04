using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePathologyOrderResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Set only when auto-billing was enabled but the charge post failed (e.g. the encounter
        // wasn't open) — the edit itself still succeeded, so this is a warning, not a Success=false.
        public string? BillingWarning { get; set; }
    }
}
