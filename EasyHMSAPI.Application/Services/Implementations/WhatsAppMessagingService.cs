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
        // Country code (digits only, no '+') prepended to 10-digit numbers.
        // Defaults to "91" (India). Override via WhatsApp:CountryCode in appsettings.
        private readonly string _countryCode;

        public WhatsAppMessagingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _isEnabled = configuration["WhatsApp:IsEnabled"] ?? string.Empty;
            _apiUrl = configuration["WhatsApp:ApiUrl"] ?? string.Empty;
            _accessToken = configuration["WhatsApp:AccessToken"] ?? string.Empty;
            _countryCode = configuration["WhatsApp:CountryCode"] ?? "91";
        }

        public async Task<bool> SendOtpAsync(string mobileNumber, string otp)
        {
            try
            {
                // Normalize to E.164 style (no '+') as required by the Meta Cloud API.
                // e.g. "9876543210" → "919876543210" (country code prepended)
                var to = NormalizeToE164(mobileNumber);

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to,
                    type = "template",
                    template = new
                    {
                        name = "otp",
                        language = new { code = "en" },
                        components = new object[]
                        {
                            // Body component — passes the OTP as the {{1}} variable
                            new
                            {
                                type = "body",
                                parameters = new object[]
                                {
                                    new { type = "text", text = otp }
                                }
                            },
                            // URL button component — "payload" type pre-fills the OTP into the
                            // autofill button (Meta requires type="payload", NOT type="text" here)
                            new
                            {
                                type = "button",
                                sub_type = "url",
                                index = "0",
                                parameters = new object[]
                                {
                                    new { type = "payload", payload = otp }
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

        // Sends login details (login id + password) to a newly added team member.
        // Requires a pre-approved WhatsApp template named "login_details" (language en) with three
        // named body variables: hosp_name, login_id, password. Until that template is approved in the
        // Meta WhatsApp Business Manager, this returns false (the caller falls back to copy/email).
        public async Task<bool> SendLoginDetailsAsync(string mobileNumber, string hospitalName, string loginId, string password)
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
                            name = "login_details",
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
                                        text = loginId,
                                        parameter_name = "login_id"
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = password,
                                        parameter_name = "password"
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

        public async Task<bool> SendDischargeNotificationAsync(string mobileNumber, string patientName, string hospitalName, string dischargeDate)
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
                            name = "discharge_notice_eng",
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
                                            text = dischargeDate,
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

        // Normalizes a mobile number to E.164 style (no leading '+') as required by the Meta
        // Cloud API. Strips non-digits, removes a leading '0', and prepends the configured
        // country code if not already present. Example: "9876543210" → "919876543210".
        private string NormalizeToE164(string mobileNumber)
        {
            var digits = new string(mobileNumber.Where(char.IsDigit).ToArray());
            // Remove a single leading zero (local dialing prefix)
            if (digits.StartsWith("0") && digits.Length > 1)
                digits = digits.Substring(1);
            // Prepend country code if not already present
            if (!digits.StartsWith(_countryCode))
                digits = _countryCode + digits;
            return digits;
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
