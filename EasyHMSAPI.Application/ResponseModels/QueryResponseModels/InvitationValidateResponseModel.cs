namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class InvitationValidateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Name { get; set; }
        public string? RoleName { get; set; }
        public string? Email { get; set; }
        public string Mobile { get; set; } = string.Empty;
    }
}
