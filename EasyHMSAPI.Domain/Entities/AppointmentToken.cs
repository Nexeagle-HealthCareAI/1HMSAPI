using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("AppointmentTokens")]
    public class AppointmentToken
    {
        [Key]
        public Guid TokenId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public Guid ApptId { get; set; }
        [Required, Column(TypeName = "date")]
        public DateTime TokenDate { get; set; }
        [Required]
        public int TokenNo { get; set; }
        [Required]
        public bool IsManual { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; }

        // OPD queue state (added for the QR check-in feature) -- see AppConstants.QueueTokenStatus_*
        // for the valid Status values.
        [Required, StringLength(20)]
        public string Status { get; set; } = "WAITING";
        [Required]
        public int SkipCount { get; set; }
        // Live ordering key, defaults to TokenNo at issuance; a skip re-numbers this (not TokenNo
        // itself, which stays the patient's permanent, display-facing token number).
        public int? QueueSequence { get; set; }
        public DateTime? ArrivedAt { get; set; }
        [StringLength(20)]
        public string? ArrivalMethod { get; set; } // 'Geofence' | 'StaffOverride'
        [Column(TypeName = "decimal(9,6)")]
        public decimal? ArrivalLatitude { get; set; }
        [Column(TypeName = "decimal(9,6)")]
        public decimal? ArrivalLongitude { get; set; }
        public DateTime? CalledAt { get; set; }
    }
}
