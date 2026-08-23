using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyReport
    {
        [Key]
        public Guid ReportId { get; set; }
        public Guid HospitalId { get; set; }
        
        public Guid OrderId { get; set; }
        public Guid? TemplateId { get; set; }
        
        public string ReportNo { get; set; } = null!;
        
        // Status: DRAFT, APPROVED
        public string Status { get; set; } = "DRAFT"; 
        
        public string? PdfBlobPath { get; set; }
        public string? PdfSha256 { get; set; }
        
        public DateTime? GeneratedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
