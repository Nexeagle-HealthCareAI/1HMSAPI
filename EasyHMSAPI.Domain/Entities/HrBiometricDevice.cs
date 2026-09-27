using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A registered attendance terminal (e.g. a ZKTeco K40 Pro) belonging to one hospital.
    /// Punches are only accepted from registered, active devices. ZKTeco terminals identify
    /// themselves by hardware serial number on every request, which is how a push is routed to a
    /// hospital. No navigation properties on purpose: these rows are always read by id/serial.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrBiometricDevices")]
    public class HrBiometricDevice
    {
        [Key]
        public Guid HrBiometricDeviceId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        /// <summary>Human label, e.g. "Main gate", "Ward 2 nurses' station".</summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        /// <summary>Hardware serial ("SN"). Globally unique: a physical device belongs to one hospital.</summary>
        [Required]
        [MaxLength(64)]
        public string SerialNumber { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Vendor { get; set; } = "ZKTECO";

        [MaxLength(100)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        /// <summary>
        /// SHA-256 (hex) of the access token used by the JSON push endpoint / an on-site bridge. The token
        /// is shown once at registration and never stored. The ZKTeco push protocol has no token field, so
        /// that path authenticates by registered serial number alone.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string TokenHash { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public DateTime? LastSeenAt { get; set; }

        [MaxLength(64)]
        public string? LastSeenIp { get; set; }

        /// <summary>Wall-clock time of the newest scan this device has delivered.</summary>
        [Column(TypeName = "datetime2(0)")]
        public DateTime? LastPunchTime { get; set; }

        /// <summary>ZKTeco push protocol: highest attendance-log stamp acknowledged, so an offline device resumes cleanly.</summary>
        [MaxLength(50)]
        public string? LastAttlogStamp { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }
}
