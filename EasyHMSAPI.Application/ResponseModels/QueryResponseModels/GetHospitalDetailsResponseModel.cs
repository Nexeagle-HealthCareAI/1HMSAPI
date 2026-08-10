using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalDetailsResponseModel
    {
        public Guid HospitalId { get; set; }
        public Guid? HospitalDepartmentMappingId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? TimeZone { get; set; }
        public bool IsActive { get; set; }
        public bool IsPubliclyListed { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        // Null until POST hospitals/{hospitalId}/generate-code is called (or auto-assigned at
        // registration). Resolves a scanned OPD QR code to this hospital.
        public string? HospitalCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public HospitalProfileStatusDto? ProfileStatus { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HospitalProfileStatusDto
    {
        public bool IsBasicInfoComplete { get; set; }
        public bool IsContactInfoComplete { get; set; }
        public bool IsLocationInfoComplete { get; set; }
        public int ProfileCompletionPercent { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
} 