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

        // Daily, per-hospital sequential token (resets every day) assigned at order creation and
        // printed on a thermal receipt for the patient -- separate from OrderNo, which keeps its
        // existing lab-accession format. Allocated via PathologyTokenHelper. Null for orders
        // created before this feature shipped.
        public int? TokenNumber { get; set; }

        // Values for the hospital's configured report-level fields (LabConfiguration.
        // ReportFieldLayoutJson's "reportFields" list) -- {key: value}, e.g. Clinical History,
        // Specimen Type. Lives on the order rather than PathologyReport so it's fillable/editable
        // before a report is ever generated and survives freely regenerating the report.
        public string? ReportFieldValuesJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
