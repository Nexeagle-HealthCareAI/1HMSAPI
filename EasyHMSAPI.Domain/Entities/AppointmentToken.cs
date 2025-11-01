using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    [Table("AppointmentTokens")]
    public class AppointmentToken
    {
        [Key]
        public Guid TokenId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public Guid ApptId { get; set; }
        [Required, Column(TypeName = "date")]
        public DateTime TokenDate { get; set; }
        [Required]
        public int TokenNo { get; set; }
        [Required]
        public bool IsManual { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; }
    }
}
