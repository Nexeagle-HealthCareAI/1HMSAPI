using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Hospital-scoped master of third-party labs a pathology test can be referred/sent out to.
    // Kept separate from Vendor (procurement's drug-license/payment-terms-flavored entity) since
    // there's nothing lab-specific to hang off it without polluting the procurement domain.
    [ExcludeFromCodeCoverage]
    public class PathologyExternalLab
    {
        [Key]
        public Guid ExternalLabId { get; set; }
        public Guid HospitalId { get; set; }

        public string LabName { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? AccreditationNo { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
