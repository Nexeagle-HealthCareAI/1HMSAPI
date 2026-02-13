using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class SmsService : ISmsService
    {
        private readonly string _twilioAccountSid;
        private readonly string _twilioAuthToken;
        private readonly string _twilioPhoneNumber;

        public SmsService(IConfiguration configuration)
        {
            _twilioAccountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
            _twilioAuthToken = configuration["Twilio:AuthToken"] ?? string.Empty;
            _twilioPhoneNumber = configuration["Twilio:PhoneNumber"] ?? string.Empty;
            TwilioClient.Init(_twilioAccountSid, _twilioAuthToken);
        }

        public async Task<bool> SendOtpSmsAsync(string mobileNumber, string otp)
        {
            try
            {
                string formattedMobileNumber = mobileNumber;
                if (!mobileNumber.StartsWith("+"))
                {
                    formattedMobileNumber = "+91" + mobileNumber.TrimStart('0');
                }
                var message = await MessageResource.CreateAsync(
                    to: new PhoneNumber(formattedMobileNumber),
                    from: new PhoneNumber(_twilioPhoneNumber),
                    body: $"Your NexEagle easyHMS verification code is: {otp}\n\nNote: NexEagle Support will not ask for this code. Do not share it with anyone."
                );

                if (message.ErrorCode == null)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send SMS: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendInvitationSmsAsync(string mobileNumber, string messageBody)
        {
            try
            {
                string formattedMobileNumber = mobileNumber;
                if (!mobileNumber.StartsWith("+"))
                {
                    formattedMobileNumber = "+91" + mobileNumber.TrimStart('0');
                }
                var message = await MessageResource.CreateAsync(
                    to: new PhoneNumber(formattedMobileNumber),
                    from: new PhoneNumber(_twilioPhoneNumber),
                    body: messageBody
                );

                return message.ErrorCode == null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send SMS: {ex.Message}");
                return false;
            }
        }
    }
}
