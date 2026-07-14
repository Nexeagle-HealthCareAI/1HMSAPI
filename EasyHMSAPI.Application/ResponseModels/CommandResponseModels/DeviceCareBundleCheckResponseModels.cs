using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SubmitDeviceCareBundleCheckResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? CheckId { get; set; }
        public int? CompliantCount { get; set; }
        public int? TotalItems { get; set; }
        public bool? AllCompliant { get; set; }
    }
}
