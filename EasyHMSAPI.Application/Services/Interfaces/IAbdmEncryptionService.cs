namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>Encrypts PII (Aadhaar number, mobile number, OTP) with ABDM's RSA public
    /// certificate before it's sent to any ABHA API, per the ABDM integrator guide.</summary>
    public interface IAbdmEncryptionService
    {
        Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken);
    }
}
