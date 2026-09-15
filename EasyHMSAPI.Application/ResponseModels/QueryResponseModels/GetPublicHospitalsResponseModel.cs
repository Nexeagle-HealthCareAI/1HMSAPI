using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicHospitalsResponseModel
    {
        public bool Success { get; set; }
        public List<PublicHospitalInfo> Hospitals { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicHospitalInfo
    {
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        // GPS pin, when the hospital has set one (HospitalBrandingConfig.tsx) -- powers a
        // "near me" map/search view on Doctor Dekho. Null when not yet configured; callers
        // (e.g. the WhatsApp bot's name matching) that don't care simply ignore these.
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
