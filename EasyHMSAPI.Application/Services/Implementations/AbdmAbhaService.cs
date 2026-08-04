using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Implements the ABHA V3 Aadhaar-OTP creation and Mobile/Aadhaar-OTP login flows against
    /// ABDM's sandbox (see the "Abdm" appsettings section for base URLs). Field/endpoint names
    /// follow the public ABDM V3 integrator guide as of this writing — if the hospital's copy of the
    /// guide names a field differently, this is the one file to patch.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbdmAbhaService : IAbdmAbhaService
    {
        private const string XTokenCachePrefix = "Abdm:XToken:";

        private readonly HttpClient _httpClient;
        private readonly IAbdmGatewayService _gatewayService;
        private readonly IAbdmEncryptionService _encryptionService;
        private readonly IMemoryCache _cache;
        private readonly string _abhaBaseUrl;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public AbdmAbhaService(
            HttpClient httpClient,
            IAbdmGatewayService gatewayService,
            IAbdmEncryptionService encryptionService,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _gatewayService = gatewayService;
            _encryptionService = encryptionService;
            _cache = cache;
            _abhaBaseUrl = (configuration["Abdm:AbhaBaseUrl"] ?? "https://abhasbx.abdm.gov.in/abha/api/v3").TrimEnd('/');
        }

        public async Task<AbdmOtpTxnResult> GenerateAadhaarOtpAsync(string aadhaarNumber, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(aadhaarNumber, cancellationToken);
            var payload = new
            {
                txnId = "",
                scope = new[] { "abha-enrol" },
                loginHint = "aadhaar",
                loginId = encrypted,
                otpSystem = "aadhaar"
            };
            var doc = await PostAsync("/enrollment/request/otp", payload, cancellationToken);
            return ReadOtpTxnResult(doc);
        }

        public async Task<AbdmEnrollResult> VerifyAadhaarOtpAsync(string txnId, string otp, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new
            {
                authData = new
                {
                    authMethods = new[] { "otp" },
                    otp = new { txnId, otpValue = encryptedOtp }
                },
                consent = new { code = "abha-enrollment", version = "1.4" }
            };
            var doc = await PostAsync("/enrollment/enrol/byAadhaar", payload, cancellationToken);
            return ReadEnrollResult(doc, txnId);
        }

        public async Task<AbdmOtpTxnResult> GenerateMobileOtpAsync(string txnId, string mobile, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(mobile, cancellationToken);
            var payload = new
            {
                txnId,
                scope = new[] { "abha-enrol", "mobile-verify" },
                loginHint = "mobile",
                loginId = encrypted,
                otpSystem = "abdm"
            };
            var doc = await PostAsync("/enrollment/request/otp", payload, cancellationToken);
            return ReadOtpTxnResult(doc, fallbackTxnId: txnId);
        }

        public async Task<AbdmEnrollResult> VerifyMobileOtpAsync(string txnId, string otp, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new
            {
                authData = new
                {
                    authMethods = new[] { "otp" },
                    otp = new { txnId, otpValue = encryptedOtp }
                }
            };
            var doc = await PostAsync("/enrollment/auth/byAbdm/verify", payload, cancellationToken);
            return ReadEnrollResult(doc, txnId);
        }

        public async Task<AbdmAddressSuggestionsResult> GetAbhaAddressSuggestionsAsync(string txnId, CancellationToken cancellationToken)
        {
            using var request = await BuildRequestAsync(HttpMethod.Get, "/enrollment/enrol/suggestion", null, cancellationToken);
            request.Headers.Add("Transaction_Id", txnId);
            var doc = await SendAsync(request, cancellationToken);

            var result = new AbdmAddressSuggestionsResult { TxnId = txnId };
            if (doc.RootElement.TryGetProperty("abhaAddressList", out var list) && list.ValueKind == JsonValueKind.Array)
                result.Suggestions = list.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList();
            return result;
        }

        public async Task<AbdmEnrollResult> CreateAbhaAddressAsync(string txnId, string abhaAddress, CancellationToken cancellationToken)
        {
            var payload = new { txnId, abhaAddress, preferred = 1 };
            var doc = await PostAsync("/enrollment/enrol/abha-address", payload, cancellationToken);
            var result = ReadEnrollResult(doc, txnId);
            if (string.IsNullOrWhiteSpace(result.AbhaAddress))
                result.AbhaAddress = abhaAddress;
            return result;
        }

        public async Task<AbdmOtpTxnResult> RequestLoginOtpAsync(string loginId, string loginHint, string otpSystem, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(loginId, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-login", loginHint == "aadhaar" ? "aadhaar-verify" : "mobile-verify" },
                loginHint,
                loginId = encrypted,
                otpSystem
            };
            var doc = await PostAsync("/profile/login/request/otp", payload, cancellationToken);
            return ReadOtpTxnResult(doc);
        }

        public async Task<AbdmProfileResult> VerifyLoginOtpAsync(string txnId, string otp, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-login" },
                authData = new
                {
                    authMethods = new[] { "otp" },
                    otp = new { txnId, otpValue = encryptedOtp }
                }
            };
            var doc = await PostAsync("/profile/login/verify", payload, cancellationToken);

            var root = doc.RootElement;
            if (root.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String)
                CacheXToken(txnId, tokenEl.GetString());

            // Sandbox returns either a single account object or an "accounts" array (one loginId can
            // map to multiple ABHA numbers) — prefer the first linked account either way.
            JsonElement account = root;
            if (root.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array && accounts.GetArrayLength() > 0)
                account = accounts[0];

            return new AbdmProfileResult
            {
                TxnId = txnId,
                AbhaNumber = ReadString(account, "ABHANumber", "abhaNumber", "healthIdNumber") ?? string.Empty,
                AbhaAddress = ReadString(account, "preferredAbhaAddress", "healthId", "abhaAddress"),
                FullName = ReadString(account, "name", "fullName") ?? string.Empty,
                Gender = ReadString(account, "gender"),
                DateOfBirth = ReadString(account, "dob", "dateOfBirth"),
                Mobile = ReadString(account, "mobile"),
                Email = ReadString(account, "email")
            };
        }

        // ---- Profile updates (require a live, freshly-verified session — see interface docs) ----

        public async Task<AbdmOtpTxnResult> RequestUpdateMobileOtpAsync(string sessionTxnId, string newMobile, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(newMobile, cancellationToken);
            var payload = new { loginId = encrypted };
            var doc = await PostAuthenticatedAsync("/profile/account/mobile/request/otp", payload, sessionTxnId, cancellationToken);
            return ReadOtpTxnResult(doc);
        }

        public async Task<AbdmUpdateResult> VerifyUpdateMobileOtpAsync(string sessionTxnId, string updateTxnId, string otp, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new { txnId = updateTxnId, otpValue = encryptedOtp };
            var doc = await PostAuthenticatedAsync("/profile/account/mobile/verify", payload, sessionTxnId, cancellationToken);
            return new AbdmUpdateResult { Success = true, Message = ReadString(doc.RootElement, "message") ?? "Mobile number updated." };
        }

        public async Task<AbdmUpdateResult> UpdateEmailAsync(string sessionTxnId, string newEmail, CancellationToken cancellationToken)
        {
            var payload = new { email = newEmail };
            var doc = await PostAuthenticatedAsync("/profile/account/email/update", payload, sessionTxnId, cancellationToken);
            return new AbdmUpdateResult { Success = true, Message = ReadString(doc.RootElement, "message") ?? "Email updated." };
        }

        public async Task<AbdmProfileResult> GetProfileAsync(string sessionTxnId, CancellationToken cancellationToken)
        {
            using var request = await BuildRequestAsync(HttpMethod.Get, "/profile/account", null, cancellationToken, RequireXToken(sessionTxnId));
            var doc = await SendAsync(request, cancellationToken);
            var root = doc.RootElement;
            return new AbdmProfileResult
            {
                TxnId = sessionTxnId,
                AbhaNumber = ReadString(root, "ABHANumber", "abhaNumber", "healthIdNumber") ?? string.Empty,
                AbhaAddress = ReadString(root, "preferredAbhaAddress", "healthId", "abhaAddress"),
                FullName = ReadString(root, "name", "fullName") ?? string.Empty,
                Gender = ReadString(root, "gender"),
                DateOfBirth = ReadString(root, "dob", "dateOfBirth"),
                Mobile = ReadString(root, "mobile"),
                Email = ReadString(root, "email"),
                ProfilePhoto = ReadString(root, "profilePhoto")
            };
        }

        public async Task<AbdmBinaryResult> GetQrCodeAsync(string sessionTxnId, CancellationToken cancellationToken)
        {
            using var request = await BuildRequestAsync(HttpMethod.Get, "/profile/account/qrCode", null, cancellationToken, RequireXToken(sessionTxnId), acceptHeader: "*/*");
            return await SendBinaryAsync(request, cancellationToken);
        }

        public async Task<AbdmBinaryResult> GetAbhaCardAsync(string sessionTxnId, CancellationToken cancellationToken)
        {
            using var request = await BuildRequestAsync(HttpMethod.Get, "/profile/account/abha-card", null, cancellationToken, RequireXToken(sessionTxnId), acceptHeader: "*/*");
            return await SendBinaryAsync(request, cancellationToken);
        }

        public async Task<AbdmFindAbhaSearchResult> FindAbhaSearchAsync(string value, string searchBy, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(value, cancellationToken);
            object payload = searchBy == "aadhaar"
                ? new { scope = new[] { "search-abha" }, aadhaar = encrypted }
                : new { scope = new[] { "search-abha" }, mobile = encrypted };
            var doc = await PostAsync("/profile/account/abha/search", payload, cancellationToken);

            // Response is a top-level array: [{ txnId, ABHA: [{ index, ABHANumber, name, gender }] }].
            var result = new AbdmFindAbhaSearchResult();
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return result;

            var entry = doc.RootElement[0];
            result.TxnId = ReadString(entry, "txnId") ?? string.Empty;
            if (entry.TryGetProperty("ABHA", out var abhaList) && abhaList.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in abhaList.EnumerateArray())
                {
                    result.Candidates.Add(new AbdmFindAbhaCandidate
                    {
                        Index = candidate.TryGetProperty("index", out var idx) && idx.TryGetInt32(out var i) ? i : 0,
                        AbhaNumber = ReadString(candidate, "ABHANumber", "abhaNumber") ?? string.Empty,
                        Name = ReadString(candidate, "name"),
                        Gender = ReadString(candidate, "gender")
                    });
                }
            }
            return result;
        }

        public async Task<AbdmOtpTxnResult> FindAbhaGenerateOtpAsync(string txnId, int index, string searchBy, CancellationToken cancellationToken)
        {
            var encryptedIndex = await _encryptionService.EncryptAsync(index.ToString(), cancellationToken);
            var verifyTag = searchBy == "aadhaar" ? "aadhaar-verify" : "mobile-verify";
            var otpSystem = searchBy == "aadhaar" ? "aadhaar" : "abdm";
            var payload = new
            {
                scope = new[] { "abha-login", "search-abha", verifyTag },
                loginHint = "index",
                loginId = encryptedIndex,
                otpSystem,
                txnId
            };
            var doc = await PostAsync("/profile/login/request/otp", payload, cancellationToken);
            return ReadOtpTxnResult(doc, fallbackTxnId: txnId);
        }

        public async Task<AbdmOtpTxnResult> RequestDeactivateOtpAsync(string sessionTxnId, string abhaNumber, string otpSystem, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(abhaNumber, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-profile", "de-activate" },
                loginHint = "abha-number",
                loginId = encrypted,
                otpSystem
            };
            var doc = await PostAuthenticatedAsync("/profile/account/request/otp", payload, sessionTxnId, cancellationToken);
            return ReadOtpTxnResult(doc);
        }

        public async Task<AbdmUpdateResult> VerifyDeactivateOtpAsync(string sessionTxnId, string deactivateTxnId, string otp, string reason, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-profile", "de-activate" },
                authData = new
                {
                    authMethods = new[] { "otp" },
                    otp = new { txnId = deactivateTxnId, otpValue = encryptedOtp }
                },
                reasons = new[] { string.IsNullOrWhiteSpace(reason) ? "Requested by ABHA holder" : reason }
            };
            var doc = await PostAuthenticatedAsync("/profile/account/verify", payload, sessionTxnId, cancellationToken);
            return new AbdmUpdateResult { Success = true, Message = ReadString(doc.RootElement, "message") ?? "ABHA number deactivated." };
        }

        public async Task<AbdmOtpTxnResult> RequestReactivateOtpAsync(string abhaNumber, CancellationToken cancellationToken)
        {
            var encrypted = await _encryptionService.EncryptAsync(abhaNumber, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-login", "mobile-verify", "re-activate" },
                loginHint = "abha-number",
                loginId = encrypted,
                otpSystem = "abdm"
            };
            // Unlike deactivate, a deactivated account has no live X-Token session to begin with —
            // this is a cold-start call authenticated only by the HIP gateway token.
            var doc = await PostAsync("/profile/account/request/otp", payload, cancellationToken);
            return ReadOtpTxnResult(doc);
        }

        public async Task<AbdmProfileResult> VerifyReactivateOtpAsync(string txnId, string otp, CancellationToken cancellationToken)
        {
            var encryptedOtp = await _encryptionService.EncryptAsync(otp, cancellationToken);
            var payload = new
            {
                scope = new[] { "abha-login", "mobile-verify", "re-activate" },
                authData = new
                {
                    authMethods = new[] { "otp" },
                    otp = new { txnId, otpValue = encryptedOtp }
                }
            };
            // Reactivate's verify step returns the same shape as a normal login (fresh X-Token +
            // linked accounts) since a successful reactivation also logs the holder in.
            var doc = await PostAsync("/profile/login/verify", payload, cancellationToken);

            var root = doc.RootElement;
            if (root.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String)
                CacheXToken(txnId, tokenEl.GetString());

            JsonElement account = root;
            if (root.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array && accounts.GetArrayLength() > 0)
                account = accounts[0];

            return new AbdmProfileResult
            {
                TxnId = txnId,
                AbhaNumber = ReadString(account, "ABHANumber", "abhaNumber", "healthIdNumber") ?? string.Empty,
                AbhaAddress = ReadString(account, "preferredAbhaAddress", "healthId", "abhaAddress"),
                FullName = ReadString(account, "name", "fullName") ?? string.Empty,
                Gender = ReadString(account, "gender"),
                DateOfBirth = ReadString(account, "dob", "dateOfBirth"),
                Mobile = ReadString(account, "mobile"),
                Email = ReadString(account, "email")
            };
        }

        // ---- HTTP plumbing -------------------------------------------------------------------

        private async Task<JsonDocument> PostAsync(string path, object payload, CancellationToken cancellationToken)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = await BuildRequestAsync(HttpMethod.Post, path, content, cancellationToken);
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>POSTs to a profile-scoped endpoint that requires the ABHA holder's own
        /// (freshly OTP-verified) X-Token, not just the HIP gateway token.</summary>
        private async Task<JsonDocument> PostAuthenticatedAsync(string path, object payload, string sessionTxnId, CancellationToken cancellationToken)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = await BuildRequestAsync(HttpMethod.Post, path, content, cancellationToken, RequireXToken(sessionTxnId));
            return await SendAsync(request, cancellationToken);
        }

        /// <summary>Looks up the cached X-Token for a session TxnId, or throws a clear
        /// "session expired" error the caller can surface as-is (the session/txn is only cached
        /// ~20 minutes after the holder's OTP verification).</summary>
        private string RequireXToken(string sessionTxnId)
        {
            if (_cache.TryGetValue(XTokenCachePrefix + sessionTxnId, out string? xToken) && !string.IsNullOrWhiteSpace(xToken))
                return xToken;
            throw new InvalidOperationException("This ABHA session has expired — please re-verify with an OTP before making changes.");
        }

        private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken, string? xToken = null, string acceptHeader = "application/json")
        {
            var accessToken = await _gatewayService.GetAccessTokenAsync(cancellationToken);
            var request = new HttpRequestMessage(method, $"{_abhaBaseUrl}{path}") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
            request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("O"));
            request.Headers.Add("X-CM-ID", "sbx");
            if (!string.IsNullOrWhiteSpace(xToken))
                request.Headers.Add("X-Token", xToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
            return request;
        }

        private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"ABDM request to {request.RequestUri} failed ({(int)response.StatusCode}): {body}");
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }

        /// <summary>Like <see cref="SendAsync"/> but for §10/§11's binary (image/PDF) responses —
        /// reads raw bytes instead of parsing JSON, passing the response's own Content-Type through
        /// unchanged so the caller doesn't have to guess between image/png and application/pdf.</summary>
        private async Task<AbdmBinaryResult> SendBinaryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = Encoding.UTF8.GetString(bytes);
                throw new InvalidOperationException($"ABDM request to {request.RequestUri} failed ({(int)response.StatusCode}): {body}");
            }
            return new AbdmBinaryResult
            {
                Content = bytes,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
            };
        }

        // ---- Response parsing ------------------------------------------------------------------

        private static AbdmOtpTxnResult ReadOtpTxnResult(JsonDocument doc, string? fallbackTxnId = null)
        {
            var root = doc.RootElement;
            return new AbdmOtpTxnResult
            {
                TxnId = ReadString(root, "txnId") ?? fallbackTxnId ?? string.Empty,
                Message = ReadString(root, "message")
            };
        }

        private void ReadEnrollTokens(JsonElement root, string txnId)
        {
            if (root.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Object)
            {
                var xToken = ReadString(tokens, "token");
                if (!string.IsNullOrWhiteSpace(xToken))
                    CacheXToken(txnId, xToken);
            }
        }

        private AbdmEnrollResult ReadEnrollResult(JsonDocument doc, string txnId)
        {
            var root = doc.RootElement;
            ReadEnrollTokens(root, txnId);

            var profile = root.TryGetProperty("ABHAProfile", out var p) && p.ValueKind == JsonValueKind.Object ? p : root;

            var firstName = ReadString(profile, "firstName");
            var middleName = ReadString(profile, "middleName");
            var lastName = ReadString(profile, "lastName");
            var fullName = ReadString(profile, "name")
                ?? string.Join(' ', new[] { firstName, middleName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return new AbdmEnrollResult
            {
                TxnId = ReadString(root, "txnId") ?? txnId,
                AbhaNumber = ReadString(profile, "abhaNumber", "ABHANumber", "healthIdNumber") ?? string.Empty,
                AbhaAddress = ReadString(profile, "healthId", "preferredAbhaAddress", "abhaAddress"),
                FullName = fullName,
                Gender = ReadString(profile, "gender"),
                DateOfBirth = ReadString(profile, "dob", "dateOfBirth"),
                Mobile = ReadString(profile, "mobile"),
                MobileVerified = profile.TryGetProperty("mobileVerified", out var mv) && mv.ValueKind == JsonValueKind.True,
                IsNew = root.TryGetProperty("isNew", out var isNew) && isNew.ValueKind == JsonValueKind.True
            };
        }

        private static string? ReadString(JsonElement element, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var s = value.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            return null;
        }

        private void CacheXToken(string txnId, string? xToken)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(xToken))
                return;
            _cache.Set(XTokenCachePrefix + txnId, xToken, TimeSpan.FromMinutes(20));
        }
    }
}
