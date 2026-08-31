using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyOrderLine
    {
        [Key]
        public Guid OrderLineId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public Guid TestId { get; set; }
        
        // Status: PENDING, SAMPLE_COLLECTED, RESULT_ENTERED. RESULT_ENTERED is terminal -- there is
        // no further approval step (the sign-off workflow was removed; a report can be freely
        // generated/regenerated from a line's results at any point after they're entered).
        public string Status { get; set; } = "PENDING";
        
        public string? SampleBarcode { get; set; }
        public DateTime? SampleCollectedAt { get; set; }
        
        public Guid? ReportId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
