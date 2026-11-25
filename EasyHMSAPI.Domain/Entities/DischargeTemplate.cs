using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A reusable discharge summary: the sections and home medicines a doctor chose to keep, saved
    /// under a name (usually the condition) and shared by the hospital. ContentJson is opaque to the
    /// server — the discharge screen decides which sections go in — and never holds a patient or
    /// admission id.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DischargeTemplate
    {
        [Key]
        public Guid DischargeTemplateId { get; set; }
        public Guid HospitalId { get; set; }
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string ContentJson { get; set; } = "{}";
        public int UseCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public Guid? UpdatedByUserId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
