using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SearchPublicHospitalsResponseModel
    {
        public bool Success { get; set; }
        // Ranked: name match, then distance, then number of doctors.
        public List<PublicHospitalMatch> Hospitals { get; set; } = new();
        public int Total { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicHospitalMatch
    {
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        // Front desk number (Contact, else AlternateContact) -- the same value doctor cards show.
        public string? Contact { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? DistanceKm { get; set; }
        public double? NameMatchScore { get; set; }
        // Doctors of this hospital live on Doctor Dekho.
        public int DoctorCount { get; set; }
    }
}
