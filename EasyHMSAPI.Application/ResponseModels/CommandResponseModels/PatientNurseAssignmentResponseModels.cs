using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AssignPatientNurseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? PatientNurseAssignmentId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReleasePatientNurseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
