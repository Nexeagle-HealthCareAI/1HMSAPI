using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsentTemplatesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ConsentTemplateItem> Templates { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ConsentTemplateItem
    {
        public Guid ConsentTemplateId { get; set; }
        public string TypeCode { get; set; } = null!;
        public string? Title { get; set; }
        public string? Language { get; set; }
        public int Version { get; set; }
        public string? BodyHtml { get; set; }
        public bool IsActive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetConsentRecordsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ConsentRecordItem> Records { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ConsentRecordItem
    {
        public Guid ConsentRecordId { get; set; }
        public string TemplateTypeCode { get; set; } = null!;
        public string? TemplateTitle { get; set; }
        public int TemplateVersion { get; set; }
        public string? ProcedureName { get; set; }
        public string SignedByName { get; set; } = null!;
        public string SignerRelation { get; set; } = null!;
        public string? WitnessName { get; set; }
        public string? WitnessRole { get; set; }
        public DateTime SignedAt { get; set; }
    }
}
