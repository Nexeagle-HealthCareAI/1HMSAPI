using System;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class DoctorTimeOff
    {
        [Key]
        public Guid TimeOffID { get; set; }
        public Guid DoctorID { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Doctor Doctor { get; set; } = null!;
    }
}
