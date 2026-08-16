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
        
        // JSON mapping the parameter names to their entered values: { "Hemoglobin": "14.2" }
        public string ResultValuesJson { get; set; } = "{}";
        
        public string? Interpretation { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
