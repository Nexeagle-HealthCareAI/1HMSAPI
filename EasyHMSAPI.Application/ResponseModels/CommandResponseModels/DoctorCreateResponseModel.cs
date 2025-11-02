using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorCreateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DoctorId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<string>? Errors { get; set; }
    }
}
