using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Insert-only snapshot of a lab result BEFORE it was overwritten. Lets an amended result show what was
    /// reported earlier, who changed it, when and why.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PathologyResultHistory
    {
        [Key]
        public Guid HistoryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid ResultId { get; set; }
        public Guid OrderLineId { get; set; }
        public Guid? ReportId { get; set; }
        public string PreviousValuesJson { get; set; } = "{}";
        public string? PreviousInterpretation { get; set; }
        public bool PreviousHasCriticalFlag { get; set; }
        public DateTime? PreviousUpdatedAt { get; set; }
        public string? PreviousUpdatedBy { get; set; }
        public string? ChangeReason { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
    }
}
