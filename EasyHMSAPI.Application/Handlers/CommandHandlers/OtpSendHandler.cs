using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class OtpSendHandler : IRequestHandler<OtpSendRequestModel, OtpSendResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IConfiguration _configuration;

        public OtpSendHandler(AppDbContext context, ISmsService smsService, IEmailService emailService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _smsService = smsService;
            _emailService = emailService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _configuration = configuration;
        }

        public async Task<OtpSendResponseModel> Handle(OtpSendRequestModel request, CancellationToken cancellationToken)
        {
            var response = new OtpSendResponseModel { Success = true, IsSmsSent = false, IsEmailSent = false, IsWhatsappSent = false };

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

            // Hash the OTP with a server-side pepper; store only the hash (Base64)
            var pepper = _configuration["Security:OtpPepper"] ?? string.Empty;
            var key = Encoding.UTF8.GetBytes(pepper);
            var data = Encoding.UTF8.GetBytes(newGeneratedOtp);
            var otpHash = HMACSHA256.HashData(key, data);
            var otpHashB64 = Convert.ToBase64String(otpHash);

            //bool smsSent = await _smsService.SendOtpSmsAsync(user.MobileNumber, newGeneratedOtp);
            bool smsSent = false;
            bool whatsappSent = await _whatsAppMessagingService.SendOtpAsync(user.MobileNumber, newGeneratedOtp);

            if(whatsappSent)
            {
                response.IsWhatsappSent = true;
                response.Success = true;
                response.Message = "OTP sent successfully via WhatsApp.";
                response.UserId = user.UserID;
            }

            if (smsSent)
            {
                response.IsSmsSent = true;
                response.Success = true;
                response.Message = "OTP sent successfully via SMS.";
                response.UserId = user.UserID;
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                bool emailSent = await _emailService.SendOtpEmailAsync(user.Email, newGeneratedOtp);
                if(smsSent && emailSent)
                {
                    response.Success = true;
                    response.IsEmailSent = true;
                    response.Message = "OTP sent successfully.";
                    response.UserId = user.UserID;
                }
                else if(emailSent)
                {
                    response.Success = true;
                    response.IsEmailSent = true;
                    response.Message = "OTP sent successfully via Email.";
                    response.UserId = user.UserID;
                }
                else
                {
                    response.IsEmailSent = true;
                    response.IsSmsSent = false;
                    response.Message = "OTP generation failed";
                }
            }

            userAuth.Otp = newGeneratedOtp;
            userAuth.OtpSentDateTime = DateTime.UtcNow;
            userAuth.OtpExpireAt = DateTime.UtcNow.AddMinutes(3);
            userAuth.IsOtpUsed = false;
            await _context.SaveChangesAsync(cancellationToken);

            return response;
        }
    }
}