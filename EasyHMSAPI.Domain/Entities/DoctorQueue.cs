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

        // Concurrency token — two simultaneous bookings for the same doctor's first-available slot
        // both reading the same NextTokenNo and racing to increment it (a silent lost update, no
        // exception) was the root cause of duplicate/skipped token numbers. EF Core checks this on
        // every UPDATE and throws DbUpdateConcurrencyException if another request already advanced
        // the row first, which AllocateTokenWithLockingAsync catches and retries against a fresh read.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
