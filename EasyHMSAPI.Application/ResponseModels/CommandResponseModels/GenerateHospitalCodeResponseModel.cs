using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GenerateHospitalCodeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? HospitalCode { get; set; }
    }
}
