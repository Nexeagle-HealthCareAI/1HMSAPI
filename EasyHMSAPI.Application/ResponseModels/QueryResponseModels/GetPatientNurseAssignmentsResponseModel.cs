using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientNurseAssignmentsResponseModel
    {
        public List<PatientNurseAssignmentItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PatientNurseAssignmentItem
    {
        public Guid PatientNurseAssignmentId { get; set; }
        public Guid NurseUserId { get; set; }
        public string? NurseName { get; set; }
        public Guid AdmissionId { get; set; }
        public string ShiftCode { get; set; } = null!;
        public DateTime? ShiftDate { get; set; }
        public string StatusCode { get; set; } = null!;
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? UnassignedAt { get; set; }
        public string? UnassignedBy { get; set; }
        public string? Notes { get; set; }
    }
}
