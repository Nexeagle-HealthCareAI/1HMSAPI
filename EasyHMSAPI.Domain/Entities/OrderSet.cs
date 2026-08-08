using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Reusable, hospital-scoped bundle of CPOE order-lines (e.g. "Standard Post-Op Protocol" =
    /// a couple of medication lines + a lab line) a doctor can apply in one action instead of
    /// writing each line manually. TemplateLinesJson is a JSON array of template line objects
    /// (ItemName/Dose/Route/Frequency/DurationDays/Instructions/OrderType/IsHighAlert/Qty) --
    /// same manual-JSON-blob convention as PackageType.ComponentsJson, since a template line is
    /// only ever read/written as part of the whole set and expanded into brand-new
    /// ClinicalOrderLine rows at apply time, never queried/joined independently.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OrderSet
    {
        public Guid OrderSetId { get; set; }
        public Guid HospitalId { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = "POST_OP";
        public string? TemplateLinesJson { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
