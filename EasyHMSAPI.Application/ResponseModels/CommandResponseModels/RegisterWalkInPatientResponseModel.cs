using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RegisterWalkInPatientResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? Age { get; set; }
        public string? Sex { get; set; }
    }
}
