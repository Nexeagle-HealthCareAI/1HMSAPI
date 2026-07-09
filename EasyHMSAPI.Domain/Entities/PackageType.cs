using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PackageType
    {
        public Guid PackageTypeId { get; set; }
        public Guid HospitalId { get; set; }
        public string Name { get; set; } = null!;
        public decimal? Price { get; set; }
        // Free-text labels of what's included (e.g. "OT Med", "Ward Med", "Room Rent",
        // "Procedure") — stored as a JSON array; no per-component price.
        public string? ComponentsJson { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
