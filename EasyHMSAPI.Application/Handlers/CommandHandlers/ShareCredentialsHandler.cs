using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Sends a newly added team member their login details over the requested channel(s).
    /// Email is free-form (works as soon as SMTP is configured); WhatsApp needs the approved
    /// "login_details" template. Returns a per-channel result so the UI can report what got through.
    /// </summary>
    public class ShareCredentialsHandler : IRequestHandler<ShareCredentialsRequestModel, ShareCredentialsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;
        private readonly IWhatsAppMessagingService _whatsApp;

        public ShareCredentialsHandler(AppDbContext context, IEmailService email, IWhatsAppMessagingService whatsApp)
        {
            _context = context;
            _email = email;
            _whatsApp = whatsApp;
        }

        public async Task<ShareCredentialsResponseModel> Handle(ShareCredentialsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.MobileNumber) || string.IsNullOrWhiteSpace(request.Password))
                return Fail("Mobile number, password and hospital are required.");

            if (!request.ViaWhatsApp && !request.ViaEmail)
                return Fail("Choose at least one way to send the login details.");

            // The admin must belong to the hospital they're acting on.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
                return Fail("You don't have access to this hospital.");

            // Sharing login details is an administrator action.
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
                return Fail("Only an administrator can share login details.");

            var hospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == request.HospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "your hospital";

            var fullName = string.IsNullOrWhiteSpace(request.FullName) ? "there" : request.FullName.Trim();
            var login = request.MobileNumber.Trim();

            var response = new ShareCredentialsResponseModel();

            if (request.ViaEmail)
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    response.EmailSent = false;
                }
                else
                {
                    var html = BuildEmailBody(fullName, hospitalName, login, request.Password, request.RoleName);
                    response.EmailSent = await _email.SendInvitationEmailAsync(request.Email.Trim(), $"Your {hospitalName} login details", html);
                }
            }

            if (request.ViaWhatsApp)
            {
                response.WhatsAppSent = await _whatsApp.SendLoginDetailsAsync(login, hospitalName, login, request.Password);
            }

            var requested = new List<string>();
            var failed = new List<string>();
            if (request.ViaEmail) { requested.Add("email"); if (response.EmailSent != true) failed.Add("email"); }
            if (request.ViaWhatsApp) { requested.Add("WhatsApp"); if (response.WhatsAppSent != true) failed.Add("WhatsApp"); }

            response.Success = failed.Count == 0;
            response.Message = response.Success
                ? $"Login details sent via {string.Join(" and ", requested)}."
                : $"Could not send via {string.Join(" and ", failed)}. You can copy the details and share them directly.";

            return response;
        }

        private static string BuildEmailBody(string fullName, string hospitalName, string login, string password, string? roleName)
        {
            var roleLine = string.IsNullOrWhiteSpace(roleName)
                ? string.Empty
                : $"<p style='font-size:14px;color:#555;margin:0 0 16px;'>Role: <strong>{WebUtility.HtmlEncode(roleName)}</strong></p>";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background-color: #f8f9fa; padding: 24px; border-radius: 8px;'>
                        <h2 style='color: #4f46e5; margin: 0 0 16px;'>Welcome to {WebUtility.HtmlEncode(hospitalName)}</h2>
                        <p style='font-size: 15px; color: #333; margin: 0 0 8px;'>Hi {WebUtility.HtmlEncode(fullName)},</p>
                        <p style='font-size: 15px; color: #333; margin: 0 0 16px;'>Your account is ready. Use the details below to sign in.</p>
                        {roleLine}
                        <table style='border-collapse: collapse; margin: 8px 0 20px;'>
                            <tr>
                                <td style='padding:8px 16px; background:#eef2ff; color:#3730a3; font-weight:bold; border-radius:6px 0 0 6px;'>Login (mobile)</td>
                                <td style='padding:8px 16px; background:#ffffff; color:#111; border:1px solid #eef2ff;'>{WebUtility.HtmlEncode(login)}</td>
                            </tr>
                            <tr>
                                <td style='padding:8px 16px; background:#eef2ff; color:#3730a3; font-weight:bold; border-radius:0 0 0 6px;'>Password</td>
                                <td style='padding:8px 16px; background:#ffffff; color:#111; border:1px solid #eef2ff;'>{WebUtility.HtmlEncode(password)}</td>
                            </tr>
                        </table>
                        <p style='font-size: 13px; color: #666; margin: 0 0 4px;'>Please change your password after your first login.</p>
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                        <p style='font-size: 12px; color: #999; margin: 0;'>This is an automated message. Please do not reply to this email.</p>
                    </div>
                </div>";
        }

        private static ShareCredentialsResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
