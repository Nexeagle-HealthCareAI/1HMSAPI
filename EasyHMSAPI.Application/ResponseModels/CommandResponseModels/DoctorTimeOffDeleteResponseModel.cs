using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffDeleteResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? TimeOffId { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
