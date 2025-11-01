namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string recipientEmail, string otp);
        Task<bool> SendInvitationEmailAsync(string recipientEmail, string subject, string htmlBody);
    }
}
