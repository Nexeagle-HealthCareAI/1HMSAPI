using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorByIdResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public PublicDoctorInfo? Doctor { get; set; }
    }
}
