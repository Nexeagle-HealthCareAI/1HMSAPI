using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionMedicine
    {
        [Key]
        public Guid PresMedicineId { get; set; }
        public Guid PrescriptionId { get; set; }
        public string? MedicineName { get; set; }
        public string? Instructions { get; set; }
        public string? Frequency { get; set; }
        public string? Dosage { get; set; }
        public string? Route { get; set; }
        public string? SaltName { get; set; }
        public string? Durations { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdateBy { get; set; }
        public byte[]? RowVersion { get; set; }
        public int? DisplayOrder { get; set; }
    }
}
