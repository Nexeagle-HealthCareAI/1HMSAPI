using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AdminUpdateUserResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public Guid? UserId { get; set; }
    }
}
