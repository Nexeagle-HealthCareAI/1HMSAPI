using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class InvitationUpdateHandler : IRequestHandler<InvitationUpdateRequestModel, InvitationUpdateResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        public readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _registrationBaseUrl;

        public InvitationUpdateHandler(AppDbContext context, ISmsService smsService, IEmailService emailService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _smsService = smsService;
            _emailService = emailService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _registrationBaseUrl = configuration["Invitation:RegistrationBaseUrl"] ?? string.Empty;
        }

        public async Task<InvitationUpdateResponseModel> Handle(InvitationUpdateRequestModel request, CancellationToken cancellationToken)
        {
            var invitation = await _context.UserInvitations
                .FirstOrDefaultAsync(i => i.InvitationID == request.InvitationId, cancellationToken);
            var currentDateTime = DateTime.Now;

            if (invitation == null)
            {
                return new InvitationUpdateResponseModel
                {
                    Success = false,
                    InvitationId = request.InvitationId,
                    Status = "NotFound",
                    Message = "Invitation not found."
                };
            }

            string scope = request.Scope?.Trim().ToLowerInvariant() ?? string.Empty;
            if (scope == "resend")
            {
                byte[] rawTokenBytes = RandomNumberGenerator.GetBytes(32);
                string rawToken = Convert.ToBase64String(rawTokenBytes);
                byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

                invitation.TokenHash = tokenHash;
                invitation.ExpiresAt = DateTime.UtcNow.AddDays(1);
                invitation.AcceptedAt = null;
                invitation.RevokedAt = null;
                invitation.Status = "Pending";

                await _context.SaveChangesAsync(cancellationToken);

                string registrationUrl = _registrationBaseUrl + Uri.EscapeDataString(rawToken);
                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == invitation.HospitalID)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                var roleName = await _context.Roles
                    .Where(r => r.RoleID == invitation.RoleID)
                    .Select(r => r.RoleName)
                    .FirstOrDefaultAsync(cancellationToken);

                //var smsMsg = $"Your easyHMS registration link has been reissued: {registrationUrl} (valid 24 hours)";
                //_ = _smsService.SendInvitationSmsAsync(invitation.RecipientMobile, smsMsg);
                await _whatsAppMessagingService.SendInvitationAsync(invitation.RecipientMobile, hospitalName ?? string.Empty
                    , roleName ?? string.Empty, registrationUrl);

                if (!string.IsNullOrWhiteSpace(invitation.RecipientEmail))
                {
                    var html = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 640px; margin: 0 auto; padding: 24px; background:#f8f9fa;'>
                          <div style='background:#ffffff; border-radius:10px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
                            <div style='background:#007bff; padding:18px 24px;'>
                              <h2 style='margin:0; color:#ffffff; font-weight:600; font-size:20px;'>Your easyHMS link is ready</h2>
                            </div>
                            <div style='padding:24px 24px 8px 24px;'>
                              <p style='margin:0 0 12px 0; color:#333; font-size:16px;'>Hello {invitation.RecipientName ?? "there"},</p>
                              <p style='margin:0 0 16px 0; color:#444; line-height:1.6;'>Your <strong>registration link</strong> has been reissued. Click the button below to continue your setup.</p>
                              <div style='text-align:center; margin:24px 0;'>
                                <a href='{registrationUrl}' style='display:inline-block; background:#007bff; color:#ffffff; text-decoration:none; padding:12px 20px; border-radius:6px; font-weight:600;'>Continue Registration</a>
                              </div>
                              <p style='margin:0 0 8px 0; color:#666; font-size:14px;'>This link is valid for <strong>24 hours</strong>. If the button doesn't work, copy and paste this URL into your browser:</p>
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
                    _ = _emailService.SendInvitationEmailAsync(invitation.RecipientEmail!, "NextEagle easyHMS Registration Link", html);
                }

                return new InvitationUpdateResponseModel
                {
                    Success = true,
                    InvitationId = invitation.InvitationID,
                    NewRegistrationUrl = registrationUrl,
                    Status = invitation.Status,
                    ExpiresAt = invitation.ExpiresAt,
                    Message = "Invitation link resent."
                };
            }
            else if (scope == "revoke")
            {
                invitation.RevokedAt = currentDateTime;
                invitation.ExpiresAt = currentDateTime;
                invitation.Status = "Revoked";

                var userExists = await _context.Users
                    .Where(x => x.MobileNumber == invitation.RecipientMobile && x.UserStatusId != (int)UserStatusEnum.Revoked)
                    .FirstOrDefaultAsync();
                if(userExists != null)
                {
                    var userAuth = await _context.UserAuths
                        .Where(ua => ua.UserID == userExists.UserID)
                        .FirstOrDefaultAsync();
                    var userProfile = await _context.UserProfiles
                        .Where(up => up.UserID == userExists.UserID)
                        .FirstOrDefaultAsync();

                    userExists.UserStatusId = (int)UserStatusEnum.Revoked;
                    if (userAuth != null)
                    {
                        userAuth.IsLocked = true;
                        userAuth.UserStatusId = (int)UserStatusEnum.Revoked;
                    }
                    if (userProfile != null)
                    {
                        userProfile.UserStatusId = (int)UserStatusEnum.Revoked;
                    }

                    var userHistory = new UserHistory
                    {
                        UserId = userExists.UserID,
                        UserStatusId = (int)UserStatusEnum.Revoked,
                        UpdatedBy = request.PerformedByUserId,
                        UpdatedDate = currentDateTime
                    };
                    _context.UserHistories.Add(userHistory);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new InvitationUpdateResponseModel
                {
                    Success = true,
                    InvitationId = invitation.InvitationID,
                    Status = invitation.Status,
                    ExpiresAt = invitation.ExpiresAt,
                    Message = "Invitation revoked."
                };
            }

            return new InvitationUpdateResponseModel
            {
                Success = false,
                InvitationId = request.InvitationId,
                Status = invitation.Status,
                Message = "Invalid scope. Use 'resend' or 'revoke'."
            };
        }
    }
}
