using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMyChainResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        // Null when the caller does not own a chain yet.
        public Guid? ChainId { get; set; }
        public string? ChainName { get; set; }
        public List<ChainHospitalItem> Hospitals { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ChainHospitalItem
    {
        public Guid HospitalId { get; set; }
        public string Name { get; set; } = null!;
        public string? City { get; set; }
        public string? State { get; set; }
        public bool IsActive { get; set; }
    }
}
