using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One high-frequency IPD vital-signs reading (one row per reading, not per shift). Distinct
    /// from AppointmentVitals (single-shot OPD vitals stored as one row per appointment).
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("VitalReading")]
    public class VitalReading
    {
        [Key]
        public Guid VitalReadingId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public Guid? RecordedByUserId { get; set; }

        public decimal? Temperature { get; set; }
        public string? TemperatureUnit { get; set; }   // C / F
        public int? Pulse { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? RespiratoryRate { get; set; }
        public decimal? SpO2 { get; set; }

        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI { get; set; }

        public int? GcsEye { get; set; }
        public int? GcsVerbal { get; set; }
        public int? GcsMotor { get; set; }
        public int? GcsTotal { get; set; }

        public int? PainScore { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
