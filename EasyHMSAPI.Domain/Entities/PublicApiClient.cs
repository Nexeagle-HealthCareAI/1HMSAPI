using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("PublicApiClient")]
    public class PublicApiClient
    {
        [Key]
        public Guid ApiClientId { get; set; }

        public Guid HospitalId { get; set; }

        public string? ClientName { get; set; }
        public string ApiKeyHash { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime? LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
