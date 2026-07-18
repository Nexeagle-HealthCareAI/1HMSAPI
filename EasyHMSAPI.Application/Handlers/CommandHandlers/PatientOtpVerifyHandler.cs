using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Verifies a patient's WhatsApp OTP and issues a patient-scoped JWT. This token is
    // DELIBERATELY never validated via the app's standard [Authorize]/JWT-bearer pipeline (see
    // PatientTokenValidator) — it's signed with the same key as staff tokens (reusing
    // IJwtAuthService), but staff endpoints must never accept it. The "scope"=patient_public claim
    // plus manual, endpoint-local validation is what keeps the two identity spaces apart.
    public class PatientOtpVerifyHandler : IRequestHandler<PatientOtpVerifyRequestModel, PatientOtpVerifyResponseModel>
    {
        private const int MaxFailedAttempts = 5;

        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;
        private readonly IMaskingService _maskingService;

        public PatientOtpVerifyHandler(AppDbContext context, IJwtAuthService jwtAuthService, IMaskingService maskingService)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
            _maskingService = maskingService;
        }

        public async Task<PatientOtpVerifyResponseModel> Handle(PatientOtpVerifyRequestModel request, CancellationToken cancellationToken)
        {
            var mobile = request.MobileNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mobile) || string.IsNullOrEmpty(request.Otp))
            {
                return new PatientOtpVerifyResponseModel { Success = false, Message = "Mobile number and OTP are required." };
            }

            var auth = await _context.PublicPatientAuths.FirstOrDefaultAsync(a => a.Mobile == mobile, cancellationToken);
            if (auth == null)
            {
                return new PatientOtpVerifyResponseModel { Success = false, Message = "Invalid or expired OTP." };
            }

            if (auth.IsLocked)
            {
                return new PatientOtpVerifyResponseModel { Success = false, Message = "Too many incorrect attempts. Please request a new OTP." };
            }

            var now = DateTime.UtcNow;
            var isExpired = !auth.OtpExpireAt.HasValue || auth.OtpExpireAt.Value < now;

            bool isMatch = false;
            if (!string.IsNullOrEmpty(auth.Otp))
            {
                try
                {
                    isMatch = _maskingService.IsMaskingEnabled()
                        ? auth.Otp == _maskingService.Mask(request.Otp)
                        : auth.Otp == request.Otp;
                }
                catch
                {
                    isMatch = false;
                }
            }

            if (isMatch && !auth.IsOtpUsed && !isExpired)
            {
                auth.IsOtpUsed = true;
                auth.IsLocked = false;
                auth.FailedAttempts = 0;
                auth.UpdatedAt = now;
                await _context.SaveChangesAsync(cancellationToken);

                // Plain custom claim names throughout, deliberately NOT ClaimTypes.MobilePhone —
                // this token is only ever read back by PatientTokenValidator's own manual
                // JwtSecurityTokenHandler call, never through ASP.NET Core's [Authorize]/JWT-bearer
                // pipeline, so there's no reason to use a "well-known" claim URI that Microsoft.
                // IdentityModel's inbound/outbound claim-type maps might rewrite. A custom string
                // is guaranteed to round-trip byte-for-byte with no remapping ambiguity.
                var claims = new List<Claim>
                {
                    new("mobile", mobile),
                    new("scope", "patient_public"),
                    new("sessionEpoch", auth.SessionEpoch.ToString()),
                };
                var accessToken = _jwtAuthService.GenerateJwtToken(claims);

                return new PatientOtpVerifyResponseModel
                {
                    Success = true,
                    Message = "OTP verified successfully.",
                    AccessToken = accessToken,
                    Mobile = mobile,
                };
            }

            if (isExpired)
            {
                return new PatientOtpVerifyResponseModel { Success = false, Message = "OTP has expired. Please request a new one." };
            }

            auth.FailedAttempts += 1;
            if (auth.FailedAttempts >= MaxFailedAttempts)
            {
                auth.IsLocked = true;
            }
            auth.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return new PatientOtpVerifyResponseModel
            {
                Success = false,
                Message = auth.IsLocked ? "Too many incorrect attempts. Please request a new OTP." : "Invalid or already used OTP.",
            };
        }
    }
}
