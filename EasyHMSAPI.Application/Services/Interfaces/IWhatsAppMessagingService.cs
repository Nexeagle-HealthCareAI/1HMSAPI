namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IWhatsAppMessagingService
    {
        Task<bool> SendOtpAsync(string mobileNumber, string otp);
    }
}
