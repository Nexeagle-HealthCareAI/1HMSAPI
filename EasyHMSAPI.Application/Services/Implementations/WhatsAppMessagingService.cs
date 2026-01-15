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
        private readonly string _phoneNumberId;
        private readonly string _accessToken;
        private readonly string _apiVersion;

        public WhatsAppMessagingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _phoneNumberId = "917702338094533";
            _accessToken = "EAAUmI8VYHh4BQd31SXZCATub5xlJS8NCRTem4mtKiveq3ihvVrLObOcUxBfKGrCCJpI4QLcClY86qc5sqpD5aYO2y94mHrdOU559HhjS9CEHpyPOtFODEzgTmdzMM8mhXtFDTK5wUWXyvSe5NQGUAVhlTZAj36jMISBZAvBZA29QJ7Kdz65Bfnk4IjdcvuVxHQZDZD";
            _apiVersion = "v22.0";
        }

        public async Task<bool> SendOtpAsync(string mobileNumber, string otp)
        {
            try
            {
                var url = $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendInvitationAsync(string mobileNumber, string hospitalName, string role, string registrationUrl)
        {
            try
            {
                var url = $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = mobileNumber,
                    type = "template",
                    template = new
                    {
                        name = "role_access_setup",
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
                                        text = hospitalName,
                                        parameter_name = "hosp_name"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = role,
                                        parameter_name = "role"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = registrationUrl,
                                        parameter_name = "url"
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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendAppointmentConfirmationAsync(string mobileNumber, string patientName, string hospitalName, string doctorName, string tokenNumber, string appointmentDate)
        {
            try
            {
                var url = $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = mobileNumber,
                    type = "template",
                    template = new
                    {
                        name = "appointment_sent_eng",
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
                                        text = patientName,
                                        parameter_name = "patient_name"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = hospitalName,
                                        parameter_name = "hospital_name"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = doctorName,
                                        parameter_name = "doctor_name"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = tokenNumber,
                                        parameter_name = "token_num"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = appointmentDate,
                                        parameter_name = "date"
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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendPrescriptionAsync(string mobileNumber, string documentLink, string filename, string hospitalName, string doctorName)
        {
            try
            {
                var url = $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = mobileNumber,
                    type = "template",
                    template = new
                    {
                        name = "prescription_sent_doctor_note",
                        language = new
                        {
                            code = "en"
                        },
                        components = new object[]
                        {
                            new
                            {
                                type = "header",
                                parameters = new object[]
                                {
                                    new
                                    {
                                        type = "document",
                                        document = new
                                        {
                                            link = documentLink,
                                            filename = filename
                                        }
                                    }
                                }
                            },
                            new
                            {
                                type = "body",
                                parameters = new object[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = hospitalName,
                                        parameter_name = "hospital_name"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = doctorName,
                                        parameter_name = "dotor_name"
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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

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

