using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ChangeAdmittingDoctorResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AssignmentId { get; set; }
        public Guid? DoctorId { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}
