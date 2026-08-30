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
        
        // Status: DRAFT -> TECH_SIGNED (technician sign-off) -> APPROVED (pathologist approval)
        public string Status { get; set; } = "DRAFT";
        
        public string? PdfBlobPath { get; set; }
        public string? PdfSha256 { get; set; }
        
        public DateTime? GeneratedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedByUserId { get; set; }

        // Dual sign-off: technician sign-off happens first (transitions DRAFT -> a
        // technician-signed state), pathologist approval finalizes (-> APPROVED). Both identities
        // are captured at their own sign-off time, not derived later, so the PDF's signature block
        // always reflects who actually signed even if their profile changes afterward.
        public Guid? TechnicianUserId { get; set; }
        public string? TechnicianName { get; set; }
        public string? TechnicianRegNo { get; set; }
        public DateTime? TechnicianSignedAt { get; set; }

        public Guid? PathologistDoctorId { get; set; }
        public string? PathologistName { get; set; }
        public string? PathologistRegNo { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
