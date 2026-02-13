using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using EasyHMSAPI.Application.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class EmailService : IEmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _appPassword;

        public EmailService(IConfiguration configuration)
        {
            _smtpServer = configuration["Smtp:Server"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(configuration["Smtp:Port"], out int port) ? port : 587;
            _senderEmail = configuration["Smtp:SenderEmail"] ?? string.Empty;
            _appPassword = configuration["Smtp:AppPassword"] ?? string.Empty;
        }

        public async Task<bool> SendOtpEmailAsync(string recipientEmail, string otp)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_senderEmail));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "Your OTP Verification Code - NexEagle easyHMS";

                var builder = new BodyBuilder();
                builder.HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px;'>
                            <h2 style='color: #007bff; margin-bottom: 20px;'>OTP Verification Code</h2>
                            <p style='font-size: 16px; color: #333; margin-bottom: 20px;'>
                                Your NexEagle easyHMS verification code is:
                            </p>
                            <div style='background-color: #007bff; color: white; padding: 15px; border-radius: 5px; text-align: center; margin: 20px 0;'>
                                <h1 style='margin: 0; font-size: 32px; letter-spacing: 5px;'>{otp}</h1>
                            </div>
                            <p style='font-size: 14px; color: #666; margin-top: 20px;'>
                                <strong>Important:</strong> This code will expire in 10 minutes. 
                                NexEagle Support will never ask for this code. Do not share it with anyone.
                            </p>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                            <p style='font-size: 12px; color: #999;'>
                                This is an automated message. Please do not reply to this email.
                            </p>
                        </div>
                    </div>";

                email.Body = builder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_senderEmail, _appPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendInvitationEmailAsync(string recipientEmail, string subject, string htmlBody)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_senderEmail));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };
                email.Body = builder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_senderEmail, _appPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                return false;
            }
        }
    }
}
