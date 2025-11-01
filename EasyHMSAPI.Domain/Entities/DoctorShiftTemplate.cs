using System;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class DoctorShiftTemplate
    {
        [Key]
        public Guid TemplateID { get; set; }
        [MaxLength(50)]
        public string? ShiftName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int SlotDurationInMinutes { get; set; } = 15;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
