namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class InvitationUpdateResponseModel
    {
        public bool Success { get; set; }
        public Guid InvitationId { get; set; }
        public string? NewRegistrationUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public string? Message { get; set; }
    }
}
