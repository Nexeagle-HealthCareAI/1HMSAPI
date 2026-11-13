using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // What a hospital tells a Doctor Dekho patient on the day of the visit (Vita vision, Phase 4): landmark, entrance, OPD location,
    // registration desk, parking, payment, a note. 1HMS portal -> Settings -> Hospital Info -> Patient visit guide.
    [ExcludeFromCodeCoverage]
    [Table("HospitalVisitGuides")]
    public class HospitalVisitGuide
    {
        [Key]
        public Guid HospitalId { get; set; }
        public string? Landmark { get; set; }
        public string? EntranceNote { get; set; }
        public string? OpdLocation { get; set; }
        public string? RegistrationDesk { get; set; }
        public string? ParkingNote { get; set; }
        public string? PaymentNote { get; set; }
        public string? ExtraNote { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedByUserId { get; set; }
    }
}
