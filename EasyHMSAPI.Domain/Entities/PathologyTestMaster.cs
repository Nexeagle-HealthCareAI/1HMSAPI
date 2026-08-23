using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyTestMaster
    {
        [Key]
        public Guid TestId { get; set; }
        public Guid HospitalId { get; set; }
        
        public string TestCode { get; set; } = null!;
        public string TestName { get; set; } = null!;
        public string? Category { get; set; }
        
        // Link to Billing
        public Guid? ChargeId { get; set; } 
        
        public string? SampleType { get; set; }
        public string? ContainerType { get; set; }
        
        // JSON defining expected result parameters: { "params": [{"name": "Hemoglobin", "unit": "g/dL", "min": 13, "max": 17 }] }
        public string? ParameterSchemaJson { get; set; } 
        
        public Guid? DefaultTemplateId { get; set; }
        
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
