using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMyHospitalsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public List<MyHospitalItem> Hospitals { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MyHospitalItem
    {
        public Guid HospitalId { get; set; }
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public bool IsPrimary { get; set; }
        public string? EmployeeId { get; set; }
        public Guid? ChainId { get; set; }
        public string? ChainName { get; set; }
        public bool IsChainOwner { get; set; }
    }
}
