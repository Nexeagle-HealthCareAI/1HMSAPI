using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertConsentTemplateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ConsentTemplateId { get; set; }
        public int Version { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SignConsentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ConsentRecordId { get; set; }
    }
}
