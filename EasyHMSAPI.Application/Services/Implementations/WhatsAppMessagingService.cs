using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Policy;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class WhatsAppMessagingService : IWhatsAppMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _isEnabled;
        private readonly string _apiUrl;
        private readonly string _accessToken;

        public WhatsAppMessagingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _isEnabled = configuration["WhatsApp:IsEnabled"] ?? string.Empty;
            _apiUrl = configuration["WhatsApp:ApiUrl"] ?? string.Empty;
            _accessToken = configuration["WhatsApp:AccessToken"] ?? string.Empty;
        }

        public async Task<bool> SendOtpAsync(string mobileNumber, string otp)
        {
            try
            {
                if(!string.IsNullOrEmpty(_isEnabled) && _isEnabled.Trim().ToLower() == "true")
                {
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

                    var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                    {
                        Content = content
                    };
                    request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                    var response = await _httpClient.SendAsync(request);

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    return false;
                }
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
                if (!string.IsNullOrEmpty(_isEnabled) && _isEnabled.Trim().ToLower() == "true")
                {
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

                    var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                    {
                        Content = content
                    };
                    request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                    var response = await _httpClient.SendAsync(request);

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    return false;
                } 
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendAppointmentConfirmationAsync(string mobileNumber, string patientName, string hospitalName, string doctorName, string tokenNumber, string appointmentDate, string appointmentTime)
        {
            try
            {
                if (!string.IsNullOrEmpty(_isEnabled) && _isEnabled.Trim().ToLower() == "true")
                {
                    doctorName = FormatDoctorName(doctorName);
                    var appointmentDateTime = $"{appointmentDate} at {appointmentTime}";
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
                                            text = appointmentDateTime,
                                            parameter_name = "date"
                                        }
                                    }
                                }
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                    {
                        Content = content
                    };
                    request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                    var response = await _httpClient.SendAsync(request);

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    return false;
                }   
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendPrescriptionAsync(string mobileNumber, string documentLink, string fileName, string hospitalName, string doctorName)
        {
            try
            {
                if (!string.IsNullOrEmpty(_isEnabled) && _isEnabled.Trim().ToLower() == "true")
                {
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
                                            filename = fileName
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

                    var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                    {
                        Content = content
                    };
                    request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                    var response = await _httpClient.SendAsync(request);

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    return false;
                }   
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FormatDoctorName(string doctorName)
        {
            if (string.IsNullOrWhiteSpace(doctorName))
                return doctorName;

            var trimmed = doctorName.Trim();

            if (trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(3).TrimStart();
            }
            else if (trimmed.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(2).TrimStart();
            }

            return trimmed;
        }
    }
}

