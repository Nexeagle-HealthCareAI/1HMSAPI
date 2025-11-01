using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    [Table("AppointmentVitals")]
    public class AppointmentVitals
    {
        [Key]
        public Guid VitalId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public string? PatientId { get; set; }
        [Required]
        public Guid ApptId { get; set; }
        [Required]
        public string VitalsJson { get; set; } = string.Empty;
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public short? BP_Sys { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public short? BP_Dia { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public short? Pulse { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal? TempC { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public byte? SpO2 { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public short? HeightCm { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal? WeightKg { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal? BMI { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public Guid? RecordedBy { get; set; }
        [Required]
        public DateTime RecordedAt { get; set; }
    }
}
