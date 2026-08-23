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
    }
}
