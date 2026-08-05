using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class MedicineMaster
    {
        [Key]
        public int MedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string? GenericName { get; set; }
        public string? BrandName { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? UsageDescription { get; set; }
        public string? SideEffects { get; set; }
        public Decimal? PriceApprox { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? PackSize { get; set; }
        public bool? RequiresPrescription { get; set; }
        public string? PrescriptionFormat { get; set; }
        public string? SourceKey { get; set; }
    }
}
