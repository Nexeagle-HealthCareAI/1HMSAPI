using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One theatre/time-slot booking per SurgeryCase (at most one SCHEDULED/IN_PROGRESS row per
    /// case — DB-enforced via a filtered unique index). Rescheduling updates this row in place.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("OTBooking")]
    public class OTBooking
    {
        [Key]
        public Guid OTBookingId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }
        public Guid TheatreId { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string StatusCode { get; set; } = null!;   // SCHEDULED/IN_PROGRESS/COMPLETED/CANCELLED

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
