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
        
        // Status: PENDING, SAMPLE_COLLECTED, RESULT_ENTERED, REPORT_APPROVED
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
