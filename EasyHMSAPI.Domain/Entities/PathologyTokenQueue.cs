using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("PathologyTokenQueue")]
    public class PathologyTokenQueue
    {
        // Composite key configured in AppDbContext.OnModelCreating
        public Guid HospitalId { get; set; }
        public DateTime TokenDate { get; set; }

        [Required]
        public int NextTokenNo { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Concurrency token -- two simultaneous order creations racing to increment NextTokenNo is
        // exactly the scenario DoctorQueue.RowVersion guards against; mirrors that same fix here.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
