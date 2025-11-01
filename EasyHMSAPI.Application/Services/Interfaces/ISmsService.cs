namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface ISmsService
    {
        Task<bool> SendOtpSmsAsync(string mobileNumber, string otp);
        Task<bool> SendInvitationSmsAsync(string mobileNumber, string message);
    }
}
