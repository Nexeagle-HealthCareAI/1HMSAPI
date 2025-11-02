using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationCreateResponseModel
    {
        public bool Success { get; set; }
        public Guid InvitationId { get; set; }
        public string RegistrationUrl { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}
