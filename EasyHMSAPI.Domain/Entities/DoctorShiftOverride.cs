using System;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class DoctorShiftOverride
    {
        [Key]
        public Guid OverrideID { get; set; }
        public Guid DoctorID { get; set; }
        [MaxLength(50)]
        public string? ShiftName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int SlotDurationInMinutes { get; set; } = 15;
        [MaxLength(50)]
        public string? RecurringDays { get; set; }
        public DateTime? OverrideDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Doctor Doctor { get; set; } = null!;
    }
}
