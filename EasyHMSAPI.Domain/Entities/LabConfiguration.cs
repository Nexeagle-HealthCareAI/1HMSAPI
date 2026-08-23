using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class LabConfiguration
    {
        [Key]
        public Guid ConfigId { get; set; }
        public Guid HospitalId { get; set; }
        
        public bool AutoBillOnOrder { get; set; }
        
        public string? DefaultReportHeaderBlob { get; set; }
        public string? DefaultReportFooterText { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
