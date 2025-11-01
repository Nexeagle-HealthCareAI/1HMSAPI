namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class InvitationListResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<InvitationItem> Invitations { get; set; } = new();
    }

    public class InvitationItem
    {
        public Guid InvitationId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? RecipientName { get; set; }
        public string RecipientMobile { get; set; } = string.Empty;
        public string? RecipientEmail { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
