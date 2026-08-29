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

        // OPD, IPD, EMERGENCY, WALK_IN -- EncounterId/AdmissionId alone don't cleanly distinguish
        // OPD from Emergency (both can carry an EncounterId), so the caller passes this explicitly
        // at order-creation time. Drives the Pathology Workspace's source-filter tabs.
        public string? SourceType { get; set; }

        // STAT/urgent order -- sorts to the top of the worklist and highlights in the UI.
        public bool IsStat { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
