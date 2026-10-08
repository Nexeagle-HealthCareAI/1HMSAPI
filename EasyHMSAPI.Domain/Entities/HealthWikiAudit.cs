using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // History shown in the CMS: who did what, when, on an article, contributor or topic.
    [ExcludeFromCodeCoverage]
    [Table("HealthWikiAudit")]
    public class HealthWikiAudit
    {
        public const string EntityArticle = "ARTICLE";
        public const string EntityContributor = "CONTRIBUTOR";
        public const string EntityTopic = "TOPIC";

        public const string ActorCmsUser = "CMS_USER";
        public const string ActorContributor = "CONTRIBUTOR";
        public const string ActorDoctorStaff = "DOCTOR_STAFF";
        public const string ActorSystem = "SYSTEM";

        [Key]
        public long AuditId { get; set; }
        public string EntityType { get; set; } = null!;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = null!;
        public string ActorType { get; set; } = null!;
        public string? ActorName { get; set; }
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
