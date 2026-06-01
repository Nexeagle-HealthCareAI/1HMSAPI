using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionByEncounterResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AdmissionInfo? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionInfo
    {
        public Guid AdmissionId { get; set; }
        public string AdmissionNo { get; set; } = null!;
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public DateTime AdmittedAt { get; set; }
        public DateTime? DischargedAt { get; set; }
        public string StatusCode { get; set; } = null!;
        public string? AdmissionReason { get; set; }
    }
}
