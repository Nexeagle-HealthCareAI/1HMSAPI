using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SearchPatientResponseModel
    {
        public List<PatientSearchResult> Items { get; set; } = new List<PatientSearchResult>();
        public int TotalPatients => Items.Count;
    }

    [ExcludeFromCodeCoverage]
    public class PatientSearchResult
    {
        public string? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public string? Sex { get; set; }
        public int? Age { get; set; }
        public string? AgeUnit { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public DateTime? LastRegistrationAt { get; set; }
        public Guid LastRegistrationId { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public Guid? AppointmentId { get; set; }
        public string? TokenNumber { get; set; }
    }
}
