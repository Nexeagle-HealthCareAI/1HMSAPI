using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Doctor search for Vita (the NLP router's patient agent), served to it through the MCP
    // endpoint (EasyHMSAPI.Api/Mcp). Same eligibility rules as the public directory
    // (GetPublicDoctorsHandler): a doctor appears only when live on Doctor Dekho. Every filter is
    // optional, but the MCP tool insists on at least one of Query / SpecialtyCategories / Degree /
    // Latitude+Longitude so a caller can never page through the whole directory by accident.
    // Read-only, and no patient data is ever part of a request.
    [ExcludeFromCodeCoverage]
    public class SearchPublicDoctorsRequestModel : IRequest<SearchPublicDoctorsResponseModel>
    {
        // Doctor name, matched fuzzily ("dr sarma" finds "Dr. Sharma"); see DoctorNameMatcher.
        public string? Query { get; set; }

        // dbo.MedicalSpecialities.PatientFacingCategory values exactly as GetPublicSpecialties
        // returns them (e.g. "Cardiologist") -- same values GetPublicDoctors' SpecialtyCategory takes.
        public List<string>? SpecialtyCategories { get; set; }

        // One degree token (MBBS, MD, MS, DM, MCh, DNB, BDS, MDS, DGO, DCH ...), matched as a whole
        // token of Doctor.Qualification so "MD" never matches "MDS".
        public string? Degree { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        // Only used with Latitude/Longitude; defaults to 5 km.
        public double? MaxDistanceKm { get; set; }

        public string? City { get; set; }
        public string? Language { get; set; }

        // Vita running inside one hospital's own channel: that hospital's doctors come first
        // (SameHospital = true), other doctors still follow. Like the OPD QR flow, the hospital's
        // own doctors are included even when the hospital itself has not opted into the marketplace.
        public Guid? HospitalId { get; set; }

        // With HospitalId: only that hospital's doctors (a patient asked for "Sadar Hospital Purnia"),
        // instead of that hospital first and other doctors after.
        public bool OnlyAtHospital { get; set; }

        // "female" / "male" (also "f", "m", "lady", "woman") -- a patient asking for a lady doctor.
        // Matched against UserProfile.Gender; doctors with no gender recorded never match.
        public string? Gender { get; set; }

        // Only doctors working today (IST): the same TimeOff > Override > Template rule the
        // directory's "Available today" badge uses.
        public bool AvailableToday { get; set; }

        // relevance (default) | distance | experience
        public string? Sort { get; set; }

        // 1..10, default 5.
        public int Limit { get; set; } = 5;

        // Skip this many ranked doctors first ("Show more doctors"); 0..90.
        public int Offset { get; set; }
    }
}
