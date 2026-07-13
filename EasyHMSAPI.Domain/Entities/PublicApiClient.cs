using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Platform-wide public API key (e.g. for the Nexeagle booking website) — no longer
    // scoped to one hospital. Every /public/* request resolves its own HospitalId from
    // the doctor being queried/booked (+ Hospital.IsPubliclyListed), never from this key.
    [ExcludeFromCodeCoverage]
    [Table("PublicApiClient")]
    public class PublicApiClient
    {
        [Key]
        public Guid ApiClientId { get; set; }

        // Informational only — which hospital's admin (if any) originally requested this
        // key, back when keys were hospital-scoped. Never used for authorization anymore.
        public Guid? HospitalId { get; set; }

        public string? ClientName { get; set; }
        public string ApiKeyHash { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime? LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
