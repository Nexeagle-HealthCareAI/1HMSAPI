using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A doctor's personalized discharge-summary field layout, scoped per (Doctor, Hospital):
    /// renamed/reordered/shown-hidden built-in fields plus any custom fields. The ordered field
    /// list is stored as JSON in <see cref="ConfigJson"/> so the schema can evolve freely.
    /// HospitalId is nullable — NULL rows are pre-migration legacy data (each doctor's old global
    /// layout, before this became hospital-scoped), read only as a one-time fallback when no
    /// hospital-specific row exists yet; every new save always writes a hospital-specific row.
    /// Mirrors DischargeSettings' (HospitalId, DoctorId) scoping.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DoctorDischargeFieldConfig
    {
        [System.ComponentModel.DataAnnotations.Key]
        public Guid ConfigId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid? HospitalId { get; set; }
        public string? ConfigJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
