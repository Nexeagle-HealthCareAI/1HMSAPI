using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    /// <summary>One field in a doctor's discharge-summary layout — a built-in section or a custom field.</summary>
    [ExcludeFromCodeCoverage]
    public class DischargeFieldConfigItemModel
    {
        public string Key { get; set; } = string.Empty;     // built-in key (e.g. "courseInHospital") or "cf_*" for custom
        public string? Label { get; set; }                  // display label (overrides the default for built-ins)
        public string? Type { get; set; }                   // builtin | text | paragraph | number | date | boolean | select
        public bool BuiltIn { get; set; }
        public bool ShowInPad { get; set; } = true;         // shown/editable in the Patient Workspace discharge form
        public bool ShowInPrint { get; set; } = true;       // appears on the printed discharge summary
        public int Order { get; set; }
        public List<string>? Options { get; set; }          // for type = select
    }

    [ExcludeFromCodeCoverage]
    public class GetDoctorDischargeFieldConfigResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        // Empty when the doctor has no saved layout yet — the client then applies its defaults.
        public List<DischargeFieldConfigItemModel> Fields { get; set; } = new();
    }
}
