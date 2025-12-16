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
        public Guid HospitalId { get; set; } // Added hospitalId
        public string? BrandName { get; set; }
        public string GenericName { get; set; } = string.Empty;
        public string? Form { get; set; }
        public string? StrengthValue { get; set; }
        public string? StrengthUnit { get; set; }
        public string? Route { get; set; }
        public string? Dose { get; set; }
        public string? Frequency { get; set; }
        public string? DurationValue { get; set; }
        public string? DurationUnit { get; set; }
        public string? Indication { get; set; }
        public string? Notes { get; set; }
        public string? MedicineId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
        public int? UsageCount { get; set; }
    }
}