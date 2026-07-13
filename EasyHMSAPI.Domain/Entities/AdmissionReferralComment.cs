using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Lightweight, insert-only, timestamped/author-attributed comment a front-desk/triage user can
    /// add against a Referred Admissions board row (AdmissionReferral) -- separate from
    /// AdmissionReferralStatusHistory, which is a silent status-transition audit trail, not a
    /// user-visible comment thread.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionReferralComment")]
    public class AdmissionReferralComment
    {
        [Key]
        public Guid CommentId { get; set; }
        public Guid ReferralId { get; set; }
        public Guid HospitalId { get; set; }
        public string CommentText { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
