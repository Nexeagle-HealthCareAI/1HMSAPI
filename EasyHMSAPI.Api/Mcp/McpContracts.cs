using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.ComponentModel;

namespace EasyHMSAPI.Api.Mcp
{
    // The JSON shapes Vita (the NLP router) receives from the MCP tools. These records ARE the
    // contract: the SDK generates each tool's output schema from them, and the router validates
    // every response against that schema. Change a field here = change the contract, so publish
    // the new tools/list output (docs/mcp/tools.json) in the same commit.

    public sealed record McpHospital(
        Guid HospitalId,
        string? Name,
        string? Address,
        string? City,
        string? State,
        string? Pincode,
        [property: Description("Hospital front-desk number, not the doctor's own")] string? Contact,
        double? Lat,
        double? Lng);

    public sealed record McpDoctor(
        Guid DoctorId,
        string? Name,
        [property: Description("Free-text qualification as entered, e.g. \"MBBS, MD (Medicine)\"")] string? Degrees,
        [property: Description("Patient-facing specialty category; same value list_specialties returns as specialtyId")] string? SpecialtyId,
        string? SpecialtyName,
        int? ExperienceYears,
        IReadOnlyList<string> Languages,
        [property: Description("OPD consultation fee in INR; null when not configured")] decimal? ConsultFee,
        [property: Description("Fee after an active discount; null when no discount is running")] decimal? DiscountedFee,
        string? PhotoUrl,
        McpHospital Hospital,
        [property: Description("Doctor's schedule has OPD today (IST)")] bool AvailableToday,
        bool OnlineNow,
        double? Rating,
        int ReviewCount,
        bool RegistrationVerified,
        [property: Description("km from the requested lat/lng; null when no location was sent")] double? DistanceKm,
        [property: Description("true when at the requested hospitalId; null when none was sent")] bool? SameHospital,
        [property: Description("0..1 name similarity; null when no name was searched")] double? NameMatchScore,
        [property: Description("Doctor Dekho profile link; null until configured")] string? ProfileUrl);

    public sealed record McpDoctorSearchResult(
        IReadOnlyList<McpDoctor> Doctors,
        [property: Description("Matches before limit was applied")] int Total);

    public sealed record McpDoctorLookup(bool Found, McpDoctor? Doctor);

    public sealed record McpSpecialty(
        [property: Description("Pass this value in search_doctors.specialtyIds")] string SpecialtyId,
        string Name,
        int DoctorCount,
        [property: Description("Other names patients use; empty until the alias table ships")] IReadOnlyList<string> Aliases);

    public sealed record McpSpecialtyList(IReadOnlyList<McpSpecialty> Specialties);

    public sealed record McpDoctorName(Guid DoctorId, string Name, string? City);

    public sealed record McpDoctorNameList(IReadOnlyList<McpDoctorName> Doctors);

    public sealed record McpHospitalMatch(
        Guid HospitalId,
        string? Name,
        string? Address,
        string? City,
        string? State,
        string? Pincode,
        [property: Description("Hospital front-desk number")] string? Contact,
        double? Lat,
        double? Lng,
        [property: Description("km from the requested lat/lng; null when no location was sent")] double? DistanceKm,
        [property: Description("0..1 name similarity; null when no name was searched")] double? NameMatchScore,
        [property: Description("Doctors of this hospital live on Doctor Dekho; list them with search_doctors(hospitalId, onlyAtHospital=true)")] int DoctorCount);

    public sealed record McpHospitalSearchResult(
        IReadOnlyList<McpHospitalMatch> Hospitals,
        [property: Description("Matches before limit was applied")] int Total);

    public sealed record McpShift(string? Name, [property: Description("HH:mm, IST")] string? Start, [property: Description("HH:mm, IST")] string? End);

    public sealed record McpAvailabilityDay(
        [property: Description("yyyy-MM-dd (IST)")] string Date,
        [property: Description("Doctor is scheduled to work this day (not on leave)")] bool IsAvailable,
        IReadOnlyList<McpShift> Shifts);

    public sealed record McpDoctorAvailability(
        bool Found,
        Guid DoctorId,
        IReadOnlyList<McpAvailabilityDay> Days,
        [property: Description("First day in the range the doctor works; null when none")] string? NextAvailableDate,
        [property: Description("1HMS has no bookable time slots for public requests: the hospital confirms the exact time")] string Note);

    internal static class McpMapping
    {
        public static McpDoctor ToMcp(this PublicDoctorInfo d, string? profileUrlTemplate,
            double? distanceKm = null, bool? sameHospital = null, double? nameMatchScore = null) =>
            new(
                d.DoctorId,
                d.FullName,
                d.Qualification,
                d.PrimaryMedicalSpecialityCategory ?? d.DepartmentName,
                d.PrimaryMedicalSpecialityPatientFacingName ?? d.PrimaryMedicalSpecialityCategory ?? d.DepartmentName,
                d.ExperienceYears,
                d.Languages,
                d.Fee,
                d.DiscountedFee,
                d.PhotoUrl,
                new McpHospital(
                    d.HospitalId, d.HospitalName, d.Address, d.City, d.State, d.Pincode, d.HospitalContact,
                    d.Latitude.HasValue ? (double)d.Latitude.Value : null,
                    d.Longitude.HasValue ? (double)d.Longitude.Value : null),
                d.IsAvailableToday,
                d.IsOnlineNow,
                d.Rating,
                d.ReviewCount,
                d.IsRegistrationVerified,
                distanceKm,
                sameHospital,
                nameMatchScore,
                string.IsNullOrWhiteSpace(profileUrlTemplate)
                    ? null
                    : profileUrlTemplate.Replace("{doctorId}", d.DoctorId.ToString(), StringComparison.Ordinal));
    }
}
