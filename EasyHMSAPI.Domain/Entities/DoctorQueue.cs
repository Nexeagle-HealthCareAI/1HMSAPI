using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
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
