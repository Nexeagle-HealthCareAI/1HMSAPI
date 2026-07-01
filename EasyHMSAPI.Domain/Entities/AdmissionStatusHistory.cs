using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Immutable admission status-transition log (append-only). Every status change writes one row.
    /// This is the source of truth for BOR / bed-turnaround / discharge-TAT KPIs — computed off the
    /// log, never a nightly snapshot.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionStatusHistory")]
    public class AdmissionStatusHistory
    {
        [Key]
        public Guid HistoryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }

        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = null!;
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public string? Reason { get; set; }
        public string? MetaJson { get; set; }
    }
}
