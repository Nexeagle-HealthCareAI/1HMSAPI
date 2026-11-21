using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class LinkAbdmFacilityServicesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FacilityId { get; set; }
        public string? HipName { get; set; }
        /// <summary>ABDM's answer, as it came.</summary>
        public string? AbdmResponse { get; set; }
    }
}
