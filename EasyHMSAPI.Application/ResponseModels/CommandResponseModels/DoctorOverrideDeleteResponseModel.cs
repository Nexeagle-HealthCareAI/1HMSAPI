using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorOverrideDeleteResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OverrideId { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
