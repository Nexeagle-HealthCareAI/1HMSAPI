using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyReportTemplate
    {
        [Key]
        public Guid TemplateId { get; set; }
        public Guid HospitalId { get; set; }
        
        public string TemplateCode { get; set; } = null!;
        public string TemplateName { get; set; } = null!;
        
        public string? HeaderBlobPath { get; set; }
        public string LayoutJson { get; set; } = "{}";
        public string? FooterText { get; set; }
        
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
