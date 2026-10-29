using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // The Doctor Dekho patient's trip to the clinic (Vita "takes you there"): how they are coming, when they plan to leave, and - only
    // with their OK - whether they are on the way, when they will arrive or how late they are, for reception. No live location is kept.
    // PlannedLeaveAt is the hospital's wall-clock (India) time like Appointment.StartAt; StartedAt, EtaAt, ArrivedAt and the rest are UTC.
    [ExcludeFromCodeCoverage]
    [Table("AppointmentJourneys")]
    public class AppointmentJourney
    {
        [Key]
        public Guid ApptId { get; set; }
        public string? TravelMode { get; set; }          // drive | auto_cab | transit | walk
        public DateTime? PlannedLeaveAt { get; set; }
        public bool ShareWithHospital { get; set; }
        public string Status { get; set; } = "planned";   // planned | on_the_way | late | arrived | cancelled
        public DateTime? StartedAt { get; set; }
        public DateTime? EtaAt { get; set; }
        public decimal? DistanceKm { get; set; }
        public int? LateByMinutes { get; set; }
        public DateTime? ArrivedAt { get; set; }
        public DateTime? LeaveReminderSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
