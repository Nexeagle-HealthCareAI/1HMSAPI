using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // WhatsApp OTP login for the public "Doctor Dekho" portal — deliberately separate from
    // OtpSendHandler (that one is hospital-STAFF login, keyed to an existing Users row; here ANY
    // well-formed mobile number gets an OTP, new or returning patient, since a first-time visitor
    // should be able to log in before they've ever booked). The response is identical either way —
    // never reveal whether a number has booking history, that's an enumeration/targeting leak.
    public class PatientOtpSendHandler : IRequestHandler<PatientOtpSendRequestModel, PatientOtpSendResponseModel>
    {
        private static readonly Regex MobileRegex = new("^[0-9]{10}$", RegexOptions.Compiled);
        private const int ResendCooldownSeconds = 30;
        private const int MaxSendsPerWindow = 5;
        private static readonly TimeSpan SendWindow = TimeSpan.FromHours(24);

        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IMaskingService _maskingService;
        private readonly IConfiguration _configuration;

        public PatientOtpSendHandler(AppDbContext context, IWhatsAppMessagingService whatsAppMessagingService, IMaskingService maskingService, IConfiguration configuration)
        {
            _configuration = configuration;
            _context = context;
            _whatsAppMessagingService = whatsAppMessagingService;
            _maskingService = maskingService;
        }

        public async Task<PatientOtpSendResponseModel> Handle(PatientOtpSendRequestModel request, CancellationToken cancellationToken)
        {
            var mobile = request.MobileNumber?.Trim() ?? string.Empty;
            if (!MobileRegex.IsMatch(mobile))
            {
                return new PatientOtpSendResponseModel { Success = false, Message = "Please enter a valid 10-digit mobile number." };
            }

            var now = DateTime.UtcNow;
            var auth = await _context.PublicPatientAuths.FirstOrDefaultAsync(a => a.Mobile == mobile, cancellationToken);
            if (auth == null)
            {
                auth = new PublicPatientAuth { Mobile = mobile, CreatedAt = now };
                _context.PublicPatientAuths.Add(auth);
            }

            // Resend cooldown — stops a fast double-tap/loop from re-triggering a WhatsApp send.
            if (auth.OtpSentAt.HasValue && (now - auth.OtpSentAt.Value).TotalSeconds < ResendCooldownSeconds)
            {
                var waitSeconds = ResendCooldownSeconds - (int)(now - auth.OtpSentAt.Value).TotalSeconds;
                return new PatientOtpSendResponseModel { Success = false, Message = $"Please wait {waitSeconds}s before requesting another OTP." };
            }

            // Rolling 24h send cap per mobile — WhatsApp template sends cost money, and an
            // unthrottled endpoint is a way to spam an arbitrary phone number with unwanted OTPs.
            if (!auth.OtpWindowStartAt.HasValue || now - auth.OtpWindowStartAt.Value > SendWindow)
            {
                auth.OtpWindowStartAt = now;
                auth.OtpSendCount = 0;
            }
            // The daily cap protects WhatsApp spend and unwanted messages. On a development server with the code shown on the page nothing is
            // sent, so the cap would only get in a tester's way (the per-IP limit still applies).
            var showOnScreen = ShowOtpOnScreen(_configuration);
            if (auth.OtpSendCount >= MaxSendsPerWindow && !showOnScreen)
            {
                return new PatientOtpSendResponseModel { Success = false, Message = "Too many OTP requests for this number. Please try again later." };
            }

            var otpInt = RandomNumberGenerator.GetInt32(100000, 1000000);
            var newOtp = otpInt.ToString();

            // On a development server the code is handed back to the page instead of being sent, so testing does not depend on WhatsApp
            // (and does not send messages from the production number). Anywhere else this is false and the code goes out on WhatsApp as before.
            if (!showOnScreen) await _whatsAppMessagingService.SendOtpAsync(mobile, newOtp);

            auth.Otp = _maskingService.Mask(newOtp);
            auth.OtpSentAt = now;
            auth.OtpExpireAt = now.AddMinutes(3);
            auth.IsOtpUsed = false;
            auth.FailedAttempts = 0;
            auth.IsLocked = false;
            auth.OtpSendCount += 1;
            auth.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return new PatientOtpSendResponseModel
            {
                Success = true,
                Message = showOnScreen ? "Development server: the OTP is shown on the page. It expires in 3 minutes." : "An OTP has been sent via WhatsApp. It expires in 3 minutes.",
                DevCode = showOnScreen ? newOtp : null,
            };
        }

        /// <summary>True only when the setting is on AND this is a Development server, so a setting copied to production by mistake still shows nothing.</summary>
        internal static bool ShowOtpOnScreen(IConfiguration configuration) =>
            string.Equals(configuration["PatientAuth:ShowOtpOnScreen"], "true", StringComparison.OrdinalIgnoreCase)
            && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
    }
}
