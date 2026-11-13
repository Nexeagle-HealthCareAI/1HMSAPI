using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SearchPublicDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Already ranked: hospital's own doctors first (when HospitalId was sent), then by name
        // match / distance / experience, then available-today first.
        public List<PublicDoctorMatch> Doctors { get; set; } = new();
        // How many doctors matched before Limit was applied.
        public int Total { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicDoctorMatch
    {
        // The exact public-safe record the Doctor Dekho directory serves.
        public PublicDoctorInfo Doctor { get; set; } = new();
        // Straight-line km from the requested Latitude/Longitude to the doctor's hospital.
        public double? DistanceKm { get; set; }
        // Only set when the request carried a HospitalId.
        public bool? SameHospital { get; set; }
        // 0..1, only set when the request carried a Query.
        public double? NameMatchScore { get; set; }
    }
}
