namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IMaskingService
    {
        string Mask(string plaintext);

        string Unmask(string maskedValue);

        bool IsMaskingEnabled();
    }
}
