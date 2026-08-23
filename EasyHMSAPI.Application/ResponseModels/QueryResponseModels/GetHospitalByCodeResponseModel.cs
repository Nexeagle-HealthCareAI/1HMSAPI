using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalByCodeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
    }
}
