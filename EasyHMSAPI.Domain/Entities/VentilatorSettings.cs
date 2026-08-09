using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Raw ventilator settings captured at a point in time -- mode, FiO2, PEEP, tidal volume, set
    /// respiratory rate, peak/plateau pressures. Insert-only, latest-wins (same shape as
    /// SofaScore), re-recorded whenever settings change or are reviewed on rounds.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("VentilatorSettings")]
    public class VentilatorSettings
    {
        [Key]
        public Guid VentilatorSettingsId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string Mode { get; set; } = null!;   // AC / SIMV / PSV / CPAP -- soft-validated free text
        public decimal? FiO2Percent { get; set; }
        public decimal? PeepCmH2o { get; set; }
        public decimal? TidalVolumeMl { get; set; }
        public int? RespiratoryRateSet { get; set; }
        public decimal? PeakInspiratoryPressure { get; set; }
        public decimal? PlateauPressure { get; set; }

        public string? Notes { get; set; }

        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
