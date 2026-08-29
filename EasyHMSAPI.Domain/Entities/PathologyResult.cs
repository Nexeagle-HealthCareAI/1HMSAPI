using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyResult
    {
        [Key]
        public Guid ResultId { get; set; }
        public Guid HospitalId { get; set; }
        
        public Guid? ReportId { get; set; }
        public Guid OrderLineId { get; set; }
        
        // JSON mapping each parameter name to its entered value AND computed flag:
        // { "Hemoglobin": { "value": "8.2", "flag": "LOW" } }. Older rows saved before the flag
        // engine existed may still be the pre-flag shape ({ "Hemoglobin": "14.2" }) -- readers must
        // handle both (see PathologyResultFlagCalculator's parsing helper).
        public string ResultValuesJson { get; set; } = "{}";

        public string? Interpretation { get; set; }

        // True when ANY parameter in ResultValuesJson computed to CRITICAL_HIGH/CRITICAL_LOW --
        // a single indexable column so "does this order have a panic value" is a WHERE clause,
        // not a JSON scan, for the DocBoard/ward-banner instant-alert query.
        public bool HasCriticalFlag { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
