using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DischargeTemplateItem
    {
        public Guid DischargeTemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContentJson { get; set; } = "{}";
        public int UseCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DischargeTemplatesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<DischargeTemplateItem> Templates { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SaveDischargeTemplateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>True when saving was refused only because the name is already taken — the screen can offer to replace.</summary>
        public bool NameTaken { get; set; }
        public DischargeTemplateItem? Template { get; set; }
    }
}
