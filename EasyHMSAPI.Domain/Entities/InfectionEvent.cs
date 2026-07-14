using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only log of a diagnosed device-associated infection (CLABSI/CAUTI/
    /// VAP) or other HAI. DeviceAssignmentId is nullable to allow logging a non-device-
    /// associated infection. Feeds the hospital-level infections-per-1000-device-days
    /// summary alongside DeviceDaysCalculator.</summary>
    [ExcludeFromCodeCoverage]
    [Table("InfectionEvent")]
    public class InfectionEvent
    {
        [Key]
        public Guid InfectionEventId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? DeviceAssignmentId { get; set; }

        public string InfectionType { get; set; } = null!;   // CLABSI / CAUTI / VAP / OTHER

        public DateTime DiagnosedAt { get; set; }
        public string DiagnosedByDoctorName { get; set; } = null!;
        public string? CultureOrganism { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
