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

        // Status is just "GENERATED" once a report exists -- the old DRAFT -> TECH_SIGNED ->
        // APPROVED sign-off pipeline was removed (GeneratePathologyReportHandler is now the only,
        // freely-repeatable step). Kept as a string column rather than dropped so historical
        // reports created under the old pipeline keep their original DRAFT/TECH_SIGNED/APPROVED
        // value on record.
        public string Status { get; set; } = "DRAFT";

        public string? PdfBlobPath { get; set; }
        public string? PdfSha256 { get; set; }

        public DateTime? GeneratedAt { get; set; }

        // The fields below are no longer written by application code -- the technician/pathologist
        // sign-off workflow they supported was removed in favor of a single repeatable "generate
        // report" action with no signature capture. Left in place (nullable, unused going forward)
        // rather than dropped, so historical reports created under the old pipeline keep their
        // signed-off identity on record instead of silently losing it to a migration.
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedByUserId { get; set; }
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
