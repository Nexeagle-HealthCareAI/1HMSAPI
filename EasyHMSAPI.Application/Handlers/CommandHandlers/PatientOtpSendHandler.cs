using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

        public PatientOtpSendHandler(AppDbContext context, IWhatsAppMessagingService whatsAppMessagingService, IMaskingService maskingService)
        {
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
            if (auth.OtpSendCount >= MaxSendsPerWindow)
            {
                return new PatientOtpSendResponseModel { Success = false, Message = "Too many OTP requests for this number. Please try again later." };
            }

            var otpInt = RandomNumberGenerator.GetInt32(100000, 1000000);
            var newOtp = otpInt.ToString();

            await _whatsAppMessagingService.SendOtpAsync(mobile, newOtp);

            auth.Otp = _maskingService.Mask(newOtp);
            auth.OtpSentAt = now;
            auth.OtpExpireAt = now.AddMinutes(3);
            auth.IsOtpUsed = false;
            auth.FailedAttempts = 0;
            auth.IsLocked = false;
            auth.OtpSendCount += 1;
            auth.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return new PatientOtpSendResponseModel { Success = true, Message = "An OTP has been sent via WhatsApp. It expires in 3 minutes." };
        }
    }
}
