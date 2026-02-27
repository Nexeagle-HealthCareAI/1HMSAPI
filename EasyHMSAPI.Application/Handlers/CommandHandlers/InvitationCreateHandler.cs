using System.Security.Cryptography;
using System.Text;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class InvitationCreateHandler : IRequestHandler<InvitationCreateRequestModel, InvitationCreateResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _registrationBaseUrl;

        public InvitationCreateHandler(AppDbContext context, ISmsService smsService, IEmailService emailService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _smsService = smsService;
            _emailService = emailService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _registrationBaseUrl = configuration["Invitation:RegistrationBaseUrl"] ?? string.Empty;
        }

        public async Task<InvitationCreateResponseModel> Handle(InvitationCreateRequestModel request, CancellationToken cancellationToken)
        {
            var existingHospital = await _context.Hospitals
                .Where(h => h.HospitalID == request.HospitalId)
                .Select(x => new
                {
                    x.HospitalID,
                    x.Name
                })
                .FirstOrDefaultAsync();
            var existingRole = await _context.Roles
                .Where(r => r.RoleID == request.RoleId)
                .Select(x => new
                {
                    x.RoleID,
                    x.RoleName
                })
                .FirstOrDefaultAsync();
            if (existingHospital is null || existingRole is null)
            {
                return new InvitationCreateResponseModel
                {
                    Success = false,
                    Message = existingHospital is null ? "Hospital not found." : "Role not found."
                };
            }

            var existingUser = await _context.Users
                .Where(x => (x.MobileNumber == request.Mobile || x.Email == request.Email) && x.UserStatusId != (int)UserStatusEnum.Revoked)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingUser != null)
            {
                return new InvitationCreateResponseModel
                {
                    Success = false,
                    Message = "A user with the provided mobile number or email already exists."
                };
            }

            byte[] rawTokenBytes = RandomNumberGenerator.GetBytes(32);
            string rawToken = Convert.ToBase64String(rawTokenBytes);
            byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

            var invitation = new UserInvitation
            {
                InvitationID = Guid.NewGuid(),
                HospitalID = request.HospitalId,
                RoleID = request.RoleId,
                InvitedByUserID = request.InvitedByUserId,
                RecipientName = request.Name,
                RecipientMobile = request.Mobile,
                RecipientEmail = request.Email,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.UserInvitations.Add(invitation);
            await _context.SaveChangesAsync(cancellationToken);

            string registrationUrl = _registrationBaseUrl + Uri.EscapeDataString(rawToken);

            //var smsMsg = $"You have been invited to easyHMS. Complete your registration: {registrationUrl} (valid for 7 days)";
            //_ = _smsService.SendInvitationSmsAsync(request.Mobile, smsMsg);
            await _whatsAppMessagingService.SendInvitationAsync(request.Mobile, existingHospital.Name, existingRole.RoleName, registrationUrl);

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var html = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 640px; margin: 0 auto; padding: 24px; background:#f8f9fa;'>
                      <div style='background:#ffffff; border-radius:10px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
                        <div style='background:#007bff; padding:18px 24px;'>
                          <h2 style='margin:0; color:#ffffff; font-weight:600; font-size:20px;'>You're invited to NexEagle easyHMS</h2>
                        </div>
                        <div style='padding:24px 24px 8px 24px;'>
                          <p style='margin:0 0 12px 0; color:#333; font-size:16px;'>Hello {request.Name},</p>
                          <p style='margin:0 0 16px 0; color:#444; line-height:1.6;'>You've been invited to join <strong>NexEagle easyHMS</strong>. Click the button below to complete your registration.</p>
                          <div style='text-align:center; margin:24px 0;'>
                            <a href='{registrationUrl}' style='display:inline-block; background:#007bff; color:#ffffff; text-decoration:none; padding:12px 20px; border-radius:6px; font-weight:600;'>Complete Registration</a>
                          </div>
                          <p style='margin:0 0 8px 0; color:#666; font-size:14px;'>This link is valid for <strong>7 days</strong>. If the button doesn't work, copy and paste this URL into your browser:</p>
                          <p style='word-break:break-all; color:#007bff; font-size:13px; margin:6px 0 0 0;'>
                            {registrationUrl}
                          </p>
                        </div>
                        <div style='padding:16px 24px 24px 24px;'>
                          <hr style='border:0; border-top:1px solid #e6e6e6; margin:0 0 12px 0;'>
                          <p style='margin:0; color:#999; font-size:12px;'>This is an automated message from NexEagle easyHMS. Please do not reply.</p>
                        </div>
                      </div>
                    </div>";
                _ = _emailService.SendInvitationEmailAsync(request.Email, "NexEagle easyHMS Registration Invitation", html);
            }

            return new InvitationCreateResponseModel
            {
                Success = true,
                InvitationId = invitation.InvitationID,
                RegistrationUrl = registrationUrl,
                Message = "Invitation created and link sent."
            };
        }
    }
}
