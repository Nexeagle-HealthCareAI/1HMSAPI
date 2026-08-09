using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicSpecialtiesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicSpecialtyInfo> Specialties { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicSpecialtyInfo
    {
        // Matches dbo.MedicalSpecialities.PatientFacingCategory verbatim — the same value
        // GetPublicDoctors' SpecialtyCategory filter expects, so a caller can list categories
        // here and pass one straight back into that filter.
        public string Category { get; set; } = string.Empty;
        // A representative PatientFacingName for display when a caller wants a nicer label
        // than the raw category bucket (e.g. "Cardiologist" for category "Cardiologist").
        public string? DisplayName { get; set; }
        public int DoctorCount { get; set; }
    }
}
