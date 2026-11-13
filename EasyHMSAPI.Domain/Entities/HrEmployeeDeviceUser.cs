using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Maps the PIN ("User ID") an employee was enrolled under on the hospital's device(s) to the
    /// EasyHMS employee. A PIN is unique per hospital: the same person keeps one PIN across that
    /// hospital's devices, and different hospitals legitimately reuse the same PINs.
    /// A separate table rather than a column on HrEmployee so an API deployed slightly ahead of the
    /// database script only breaks biometric features, not every employee read.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrEmployeeDeviceUsers")]
    public class HrEmployeeDeviceUser
    {
        [Key]
        public Guid HrEmployeeDeviceUserId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string DeviceUserId { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }
}
