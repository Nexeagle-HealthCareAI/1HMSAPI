using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Insert-only trail of the legally relevant events on a discharge summary: it was signed, the signature was
    /// withdrawn (with the reason and who had signed), or its public link was regenerated.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DischargeSummaryAudit
    {
        public const string ActionSign = "SIGN";
        public const string ActionUnsign = "UNSIGN";
        public const string ActionLinkRegenerated = "LINK_REGENERATED";

        [Key]
        public Guid AuditId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DischargeSummaryId { get; set; }
        public Guid AdmissionId { get; set; }
        public string Action { get; set; } = null!;
        public string? Reason { get; set; }
        // For UNSIGN: who had signed and when, since the summary itself is cleared.
        public string? PreviousSignedBy { get; set; }
        public DateTime? PreviousSignedAt { get; set; }
        public Guid? PerformedByUserId { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; }
    }
}
