using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class CreatePathologyOrderResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNo { get; set; }
        // Set only when auto-billing was enabled but the charge post failed (e.g. the encounter
        // wasn't open) — the order itself still succeeded, so this is a warning, not a Success=false.
        public string? BillingWarning { get; set; }
    }
}
