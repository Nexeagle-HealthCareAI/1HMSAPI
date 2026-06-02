using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AdmitPatientResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? AdmissionNo { get; set; }
        public string? PatientId { get; set; }
        public bool IsNewPatient { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public bool WasExisting { get; set; }
    }
}
