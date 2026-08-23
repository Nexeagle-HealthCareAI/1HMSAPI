using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyOrder
    {
        [Key]
        public Guid OrderId { get; set; }
        public Guid HospitalId { get; set; }
        
        public string PatientId { get; set; } = null!;
        public Guid? EncounterId { get; set; }
        public Guid? AdmissionId { get; set; }
        
        public string OrderNo { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        
        public Guid? OrderedByDoctorId { get; set; }
        public string? Notes { get; set; }
        
        // Status: PLACED, IN_PROGRESS, COMPLETED, CANCELLED
        public string Status { get; set; } = "PLACED"; 
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
