using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SubmitHospitalResponseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid ReviewId { get; set; }
    }
}
