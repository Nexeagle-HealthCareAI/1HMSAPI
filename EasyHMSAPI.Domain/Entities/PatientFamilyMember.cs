using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Someone a patient looks after on their own Doctor Dekho account (a child, a parent, a spouse), so their health records can be kept and
    // opened in one place. Owned by the account's OTP-verified mobile; not a login and not a hospital registration.
    [ExcludeFromCodeCoverage]
    [Table("PatientFamilyMembers")]
    public class PatientFamilyMember
    {
        [Key]
        public Guid MemberId { get; set; }
        public string OwnerMobile { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string? LastName { get; set; }
        public string Relation { get; set; } = null!;
        public short? Age { get; set; }
        // Y | M | D
        public string? AgeUnit { get; set; }
        // Male | Female | Other
        public string? Sex { get; set; }
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
