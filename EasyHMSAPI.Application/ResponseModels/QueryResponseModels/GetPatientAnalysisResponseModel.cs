using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientAnalysisResponseModel
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public PatientAnalysisDataModel? PatientAnalysis { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientAnalysisDataModel
    {
        public int TotalVisit { get; set; }
        public DateTime? LastVisitDate { get; set; }
        public double VisitFrequency { get; set; }
        public string? PatientTags { get; set; }
        public bool FollowUpsDue { get; set; }
        public bool NoShow { get; set; }
        public Dictionary<string, int>? DoctorConsulted { get; set; }
    }

}