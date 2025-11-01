using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    [Table("PatientRegistrations")]
    public class PatientRegistration
    {
        [Key]
        public Guid RegistrationId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public Guid? RegisteredBy { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? AgeYears { get; set; }
        public string? Sex { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? InsuranceId { get; set; }
    }
}
