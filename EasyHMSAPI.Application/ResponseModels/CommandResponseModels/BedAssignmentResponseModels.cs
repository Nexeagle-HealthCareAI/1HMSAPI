using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AssignBedResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? BedAssignmentId { get; set; }
        public Guid? BedId { get; set; }
        public DateTime? AssignedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReleaseBedResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? BedAssignmentId { get; set; }
        public DateTime? ReleasedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TransferBedResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? PreviousBedAssignmentId { get; set; }
        public Guid? NewBedAssignmentId { get; set; }
        public Guid? NewBedId { get; set; }
        public DateTime? TransferredAt { get; set; }
    }
}
