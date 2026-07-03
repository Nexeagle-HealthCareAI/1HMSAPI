using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBedBoardResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<BedBoardItem> Items { get; set; } = new();
    }

    /// <summary>One bed, live: always present (even when unoccupied); occupancy fields are null when free.</summary>
    [ExcludeFromCodeCoverage]
    public class BedBoardItem
    {
        public Guid BedId { get; set; }
        public string? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? WardType { get; set; }
        public string? FloorNo { get; set; }
        public string? RoomCode { get; set; }
        public string? RoomType { get; set; }
        public string? BedCode { get; set; }
        public string? BedName { get; set; }
        public string? StatusCode { get; set; }   // BedMaster status: AVAILABLE/OCCUPIED/CLEANING/RESERVED/BLOCKED
        public string? GenderRestriction { get; set; }
        public bool IsActive { get; set; }
        public decimal EffectiveDailyRate { get; set; }
        public int SortOrder { get; set; }

        // Occupancy — populated from the ACTIVE BedAssignment (and its Admission/Patient), else null.
        public Guid? BedAssignmentId { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? AdmissionNo { get; set; }
        public string? AdmissionType { get; set; }
        public string? PayerType { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public short? PatientAge { get; set; }
        public string? PatientSex { get; set; }
    }
}
