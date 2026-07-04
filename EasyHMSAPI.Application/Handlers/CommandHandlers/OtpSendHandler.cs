using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class OtpSendHandler : IRequestHandler<OtpSendRequestModel, OtpSendResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IConfiguration _configuration;
        private readonly IMaskingService _maskingService;

        public OtpSendHandler(AppDbContext context, IEmailService emailService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration, IMaskingService maskingService)
        {
            _context = context;
            _emailService = emailService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _configuration = configuration;
            _maskingService = maskingService;
        }

        public async Task<OtpSendResponseModel> Handle(OtpSendRequestModel request, CancellationToken cancellationToken)
        {
            var response = new OtpSendResponseModel { Success = true, IsEmailSent = false, IsWhatsappSent = false };

            if (string.IsNullOrEmpty(request.MobileNumber))
            {
                response.Success = true;
                response.Message = "Mobile number is required.";
                return response;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
            if (user == null)
            {
                response.Success = false;
                response.Message = "User not found.";
                return response;
            }

            var userAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID, cancellationToken);
            if (userAuth == null)
            {
                response.Success = true;
                response.Message = "User authentication details not found.";
                return response;
            }

            // Generate a cryptographically strong 6-digit OTP
            var otpInt = RandomNumberGenerator.GetInt32(100000, 1000000);
            var newGeneratedOtp = otpInt.ToString();

            // Mask the OTP if masking is enabled, otherwise store plaintext
            var otpToStore = _maskingService.Mask(newGeneratedOtp);

            // SMS/Twilio delivery has been removed — WhatsApp is the primary OTP channel, with
            // email as a fallback for users without a reachable WhatsApp number.
            bool whatsappSent = await _whatsAppMessagingService.SendOtpAsync(user.MobileNumber, newGeneratedOtp);
            bool emailSent = !string.IsNullOrEmpty(user.Email) && await _emailService.SendOtpEmailAsync(user.Email, newGeneratedOtp);

            response.IsWhatsappSent = whatsappSent;
            response.IsEmailSent = emailSent;

            var sentChannels = new List<string>();
            if (whatsappSent) sentChannels.Add("WhatsApp");
            if (emailSent) sentChannels.Add("Email");

            if (sentChannels.Count == 0)
            {
                // Nothing actually reached the user — don't overwrite a still-valid prior OTP or
                // reset the lockout counters for a request that delivered no usable code.
                response.Success = false;
                response.Message = "Could not send the OTP via WhatsApp or Email. Please try again or contact support.";
                return response;
            }

            response.Success = true;
            response.Message = $"OTP sent successfully via {string.Join(" and ", sentChannels)}.";
            response.UserId = user.UserID;

            userAuth.Otp = otpToStore;
            userAuth.OtpSentDateTime = DateTime.UtcNow;
            userAuth.OtpExpireAt = DateTime.UtcNow.AddMinutes(3);
            userAuth.IsOtpUsed = false;
            // Issuing a fresh OTP clears any brute-force lockout from the previous OTP window:
            // the legitimate user self-recovers by requesting a new code, and any attacker's
            // in-progress guessing is invalidated against the new code.
            userAuth.FailedLoginAttempts = 0;
            userAuth.IsLocked = false;
            await _context.SaveChangesAsync(cancellationToken);

            return response;
        }
    }
}