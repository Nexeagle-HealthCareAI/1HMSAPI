using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GenerateAadhaarOtpHandler : IRequestHandler<GenerateAadhaarOtpRequestModel, AbdmOtpTxnResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly AppDbContext _context;

        public GenerateAadhaarOtpHandler(IAbdmAbhaService abha, AppDbContext context)
        {
            _abha = abha;
            _context = context;
        }

        public async Task<AbdmOtpTxnResponseModel> Handle(GenerateAadhaarOtpRequestModel request, CancellationToken cancellationToken)
        {
            var aadhaar = (request.AadhaarNumber ?? string.Empty).Replace(" ", string.Empty);
            if (aadhaar.Length != 12 || !aadhaar.All(char.IsDigit))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter a valid 12-digit Aadhaar number." };

            // Consent first, enforced here and not only in the wizard: a direct API call cannot skip it or burn ABDM OTP quota.
            var now = DateTime.UtcNow;
            var (consent, consentError) = await AbhaConsentRules.ValidateForOtpAsync(_context, request.ConsentId, request.HospitalId, request.CallerUserId, now, cancellationToken);
            if (consent == null)
                return new AbdmOtpTxnResponseModel { Success = false, Message = consentError };

            // Count the attempt before calling ABDM so a failing upstream cannot be hammered either.
            consent.OtpRequestCount++;
            consent.LastOtpRequestedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                var result = await _abha.GenerateAadhaarOtpAsync(aadhaar, cancellationToken);
                consent.TxnId = result.TxnId;
                await _context.SaveChangesAsync(cancellationToken);
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent to the Aadhaar-linked mobile number.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
