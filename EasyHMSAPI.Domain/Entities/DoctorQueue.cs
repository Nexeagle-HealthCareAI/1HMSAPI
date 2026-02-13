using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("DoctorQueues")]
    public class DoctorQueue
    {
        // Composite key configured in AppDbContext.OnModelCreating
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public DateTime TokenDate { get; set; }

        [Required]
        public int NextTokenNo { get; set; }
        [Required, StringLength(20)]
        public string TokenStrategy { get; set; } = string.Empty;
    }
}
