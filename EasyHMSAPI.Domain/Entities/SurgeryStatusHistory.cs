using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only audit log of SurgeryCase.StatusCode transitions — mirrors AdmissionStatusHistory.</summary>
    [ExcludeFromCodeCoverage]
    [Table("SurgeryStatusHistory")]
    public class SurgeryStatusHistory
    {
        [Key]
        public Guid HistoryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = null!;
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public string? Reason { get; set; }
    }
}
