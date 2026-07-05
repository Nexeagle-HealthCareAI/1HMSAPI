using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A doctor's personalized discharge-summary field layout (global per doctor, not per hospital):
    /// renamed/reordered/shown-hidden built-in fields plus any custom fields. The ordered field
    /// list is stored as JSON in <see cref="ConfigJson"/> so the schema can evolve freely.
    /// Mirrors DoctorPrescriptionFieldConfig.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DoctorDischargeFieldConfig
    {
        [System.ComponentModel.DataAnnotations.Key]
        public Guid ConfigId { get; set; }
        public Guid DoctorId { get; set; }
        public string? ConfigJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
