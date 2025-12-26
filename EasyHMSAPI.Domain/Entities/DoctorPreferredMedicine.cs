using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorPreferredMedicine
    {
        [Key]
        public long PreferrredId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
        public string? MedicineName { get; set; }
        public string? BrandName { get; set; }
        public string? GenericName { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? Usage { get; set; }
        public string? SideEffects { get; set; }
        public int? Price { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
        public long? UsageCount { get; set; }
    }
}