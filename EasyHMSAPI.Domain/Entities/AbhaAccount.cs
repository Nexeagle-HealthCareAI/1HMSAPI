using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>ABHA account created (Aadhaar-OTP) or linked (Mobile/Aadhaar-OTP login) via the
    /// standalone ABDM module. Not tied to <see cref="PatientRegistration"/> — LinkedPatientId is an
    /// optional, unenforced pointer for manual association later.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbhaAccount")]
    public class AbhaAccount
    {
        [Key]
        public Guid AbhaAccountId { get; set; }
        public Guid HospitalId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string Source { get; set; } = "AadhaarEnrol";
        public string? LinkedPatientId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
