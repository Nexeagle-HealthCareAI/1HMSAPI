using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class WhatsAppMessagingService : IWhatsAppMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public WhatsAppMessagingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> SendOtpAsync(string mobileNumber, string otp)
        {
            try
            {
                var phoneNumberId = "917702338094533";
                var accessToken = "EAAUmI8VYHh4BQd31SXZCATub5xlJS8NCRTem4mtKiveq3ihvVrLObOcUxBfKGrCCJpI4QLcClY86qc5sqpD5aYO2y94mHrdOU559HhjS9CEHpyPOtFODEzgTmdzMM8mhXtFDTK5wUWXyvSe5NQGUAVhlTZAj36jMISBZAvBZA29QJ7Kdz65Bfnk4IjdcvuVxHQZDZD";
                var apiVersion = "v22.0";

                if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(apiVersion))
                {
                    return false;
                }

                var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = mobileNumber,
                    type = "template",
                    template = new
                    {
                        name = "otp",
                        language = new
                        {
                            code = "en"
                        },
                        components = new object[]
                        {
                            new
                            {
                                type = "body",
                                parameters = new object[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = otp
                                    }
                                }
                            },
                            new
                            {
                                type = "button",
                                sub_type = "url",
                                index = 0,
                                parameters = new object[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = otp
                                    }
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
