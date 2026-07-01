using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DischargeAdmissionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public DateTime? DischargedAt { get; set; }
        public bool BedReleased { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionStatusResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? StatusCode { get; set; }
        public bool BedReleased { get; set; }
    }
}
