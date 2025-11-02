using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffCreateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? TimeOffId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
