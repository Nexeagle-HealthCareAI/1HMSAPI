using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyHMSAPI.Application.Services.Fhir;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>A health information request, as ABDM sends it to the HIP.</summary>
    public sealed class AbdmHiRequest
    {
        public string? TransactionId { get; init; }
        public string? ConsentId { get; init; }
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public string? DataPushUrl { get; init; }
        public string? PublicKey { get; init; }
        public string? Nonce { get; init; }
    }

    /// <summary>The rules of the consent and data flow (ABDM M2), free of any database or network.</summary>
    public static class AbdmDataFlowRules
    {
        public const string ConsentNotGrantedCode = "ABDM-1062";
        public const string ConsentExpiredCode = "ABDM-1061";
        public const string InvalidRequestCode = "ABDM-1031";

        public static DateTime? Date(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;

        public static AbdmHiRequest ParseHiRequest(string? body) => new()
        {
            TransactionId = AbdmCareContextRules.FindString(body, "transactionId"),
            ConsentId = FindIn(body, "consent", "id"),
            From = Date(FindIn(body, "dateRange", "from")),
            To = Date(FindIn(body, "dateRange", "to")),
            DataPushUrl = AbdmCareContextRules.FindString(body, "dataPushUrl"),
            PublicKey = AbdmCareContextRules.FindString(body, "keyValue"),
            Nonce = AbdmCareContextRules.FindString(body, "nonce")
        };

        /// <summary>Applies a consent notification to the row for it. Returns false when the
        /// notification carries no consent id, which is the one thing we cannot work without.</summary>
        public static bool ApplyNotification(AbdmConsent consent, string? body, DateTime nowUtc)
        {
            var consentId = AbdmCareContextRules.FindString(body, "consentId");
            if (consentId == null)
                return false;
            consent.ConsentId = consentId;
            consent.Status = (AbdmCareContextRules.FindString(body, "status") ?? consent.Status).Trim().ToUpperInvariant();
            consent.UpdatedAt = nowUtc;

            // A revocation or expiry carries no detail: keep what the grant told us.
            if (!HasObject(body, "consentDetail"))
                return true;
            consent.PatientAbhaAddress = FindIn(body, "patient", "id") ?? consent.PatientAbhaAddress;
            consent.CounterpartyId = FindIn(body, "hiu", "id") ?? consent.CounterpartyId;
            consent.CounterpartyName = FindIn(body, "hiu", "name") ?? consent.CounterpartyName;
            consent.PurposeCode = FindIn(body, "purpose", "code") ?? consent.PurposeCode;
            consent.PurposeText = FindIn(body, "purpose", "text") ?? consent.PurposeText;
            consent.HiTypes = string.Join(',', Strings(body, "hiTypes"));
            consent.CareContextReferences = string.Join(',', AllStrings(body, "careContextReference"));
            consent.DateFrom = Date(FindIn(body, "dateRange", "from"));
            consent.DateTo = Date(FindIn(body, "dateRange", "to"));
            consent.ExpiresAt = Date(AbdmCareContextRules.FindString(body, "dataEraseAt"));
            return true;
        }

        /// <summary>Why records may not be sent under this consent now, or null when they may.</summary>
        public static (string Code, string Message)? Refusal(AbdmConsent? consent, DateTime nowUtc)
        {
            if (consent == null)
                return (ConsentNotGrantedCode, "No consent with this id has been granted for this facility.");
            if (consent.Status != AbdmConsentStatus.Granted)
                return (ConsentNotGrantedCode, $"This consent is {consent.Status.ToLowerInvariant()}.");
            if (consent.ExpiresAt != null && consent.ExpiresAt.Value <= nowUtc)
                return (ConsentExpiredCode, "This consent has expired.");
            return null;
        }

        /// <summary>A visit is covered when it falls inside the consent's own date range and the
        /// range this request asks for. A missing bound does not limit.</summary>
        public static bool InRange(DateTime whenUtc, AbdmConsent consent, AbdmHiRequest request)
        {
            var from = Later(consent.DateFrom, request.From);
            var to = Earlier(consent.DateTo, request.To);
            return (from == null || whenUtc >= from.Value) && (to == null || whenUtc <= to.Value);
        }

        private static DateTime? Later(DateTime? a, DateTime? b) => a == null ? b : b == null ? a : a.Value > b.Value ? a : b;
        private static DateTime? Earlier(DateTime? a, DateTime? b) => a == null ? b : b == null ? a : a.Value < b.Value ? a : b;

        /// <summary>The HIU's push URL comes from outside. It must be https and must not point
        /// back into our own network.</summary>
        public static bool IsSafePushUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;
            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                return false;
            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                var b = ip.GetAddressBytes();
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    return false;
                if (b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254))
                    return false;
            }
            return true;
        }

        public static string Md5Hex(string content) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        // ---- JSON helpers --------------------------------------------------------------------

        /// <summary>The string <paramref name="name"/> directly inside the first object called <paramref name="parent"/>, anywhere in the body.</summary>
        public static string? FindIn(string? body, string parent, string name)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var found = FindObject(doc.RootElement, parent);
                if (found == null) return null;
                foreach (var property in found.Value.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                        return string.IsNullOrWhiteSpace(property.Value.GetString()) ? null : property.Value.GetString()!.Trim();
                }
                return null;
            }
            catch (JsonException) { return null; }
        }

        private static bool HasObject(string? body, string name)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            try
            {
                using var doc = JsonDocument.Parse(body);
                return FindObject(doc.RootElement, name) != null;
            }
            catch (JsonException) { return false; }
        }

        private static JsonElement? FindObject(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Object)
                        return property.Value;
                }
                foreach (var property in element.EnumerateObject())
                {
                    var found = FindObject(property.Value, name);
                    if (found != null) return found;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindObject(item, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>The strings in the first array called <paramref name="name"/>.</summary>
        public static IReadOnlyList<string> Strings(string? body, string name)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(body)) return list;
            try
            {
                using var doc = JsonDocument.Parse(body);
                Walk(doc.RootElement, (propertyName, value) =>
                {
                    if (list.Count == 0 && string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.Array)
                        list.AddRange(value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!.Trim()));
                });
            }
            catch (JsonException) { /* unreadable: nothing */ }
            return list;
        }

        /// <summary>Every string value called <paramref name="name"/>, wherever it sits.</summary>
        public static IReadOnlyList<string> AllStrings(string? body, string name)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(body)) return list;
            try
            {
                using var doc = JsonDocument.Parse(body);
                Walk(doc.RootElement, (propertyName, value) =>
                {
                    if (string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                        list.Add(value.GetString()!.Trim());
                });
            }
            catch (JsonException) { /* unreadable: nothing */ }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Walk(JsonElement element, Action<string, JsonElement> visit)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    visit(property.Name, property.Value);
                    Walk(property.Value, visit);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    Walk(item, visit);
            }
        }
    }

    /// <summary>Posts a page of encrypted records straight to an HIU's data push URL (not via the gateway).</summary>
    public interface IAbdmDataPushClient
    {
        /// <summary>Returns the HTTP status. Throws only on a network failure.</summary>
        Task<int> PushAsync(string url, string json, CancellationToken cancellationToken);
    }

    public sealed class AbdmDataPushClient : IAbdmDataPushClient
    {
        private readonly HttpClient _httpClient;
        private readonly IAbdmGatewayService _gateway;

        public AbdmDataPushClient(HttpClient httpClient, IAbdmGatewayService gateway)
        {
            _httpClient = httpClient;
            _gateway = gateway;
        }

        public async Task<int> PushAsync(string url, string json, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _gateway.GetAccessTokenAsync(cancellationToken));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return (int)response.StatusCode;
        }
    }

    public interface IAbdmDataFlowService
    {
        /// <summary>A consent was granted, revoked or expired: store it and acknowledge.</summary>
        Task OnConsentNotifyAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        /// <summary>An HIU asks for records under a consent: check, acknowledge, send, report.</summary>
        Task OnHealthInformationRequestAsync(AbdmInbound inbound, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Consent and data flow as the HIP (abdm-m2 journey "Consent and data flow"):
    /// consent notify → on-notify; health-information request → on-request → encrypted FHIR
    /// bundles to the HIU's dataPushUrl → health-information/notify. Nothing leaves without a
    /// consent that is granted, in date, and names the care context and the record type.
    /// </summary>
    public sealed class AbdmDataFlowService : IAbdmDataFlowService
    {
        private const string OnNotifyPath = "/api/hiecm/consent/v3/request/hip/on-notify";
        private const string OnRequestPath = "/api/hiecm/data-flow/v3/health-information/hip/on-request";
        private const string NotifyPath = "/api/hiecm/data-flow/v3/health-information/notify";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly AppDbContext _db;
        private readonly IAbdmHieService _hie;
        private readonly IAbdmRecordSource _records;
        private readonly IAbdmDataPushClient _push;
        private readonly ILogger<AbdmDataFlowService> _logger;

        public AbdmDataFlowService(AppDbContext db, IAbdmHieService hie, IAbdmRecordSource records, IAbdmDataPushClient push, ILogger<AbdmDataFlowService> logger)
        {
            _db = db;
            _hie = hie;
            _records = records;
            _push = push;
            _logger = logger;
        }

        public async Task OnConsentNotifyAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var consentId = AbdmCareContextRules.FindString(inbound.RawBody, "consentId");
            if (consentId == null)
                return;

            var now = DateTime.UtcNow;
            var consent = await _db.AbdmConsents.FirstOrDefaultAsync(
                c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hip && c.ConsentId == consentId, cancellationToken);
            if (consent == null)
            {
                consent = new AbdmConsent { ConsentRowId = Guid.NewGuid(), HospitalId = hospitalId, Role = AbdmConsentRole.Hip, CreatedAt = now };
                _db.AbdmConsents.Add(consent);
            }
            AbdmDataFlowRules.ApplyNotification(consent, inbound.RawBody, now);
            await _db.SaveChangesAsync(cancellationToken);

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "consent-on-notify",
                Path = OnNotifyPath,
                HospitalId = hospitalId,
                SubjectRef = consentId,
                ExpectsCallback = false,
                Body = new
                {
                    acknowledgement = new { status = "OK", consentId },
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);
        }

        public async Task OnHealthInformationRequestAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var request = AbdmDataFlowRules.ParseHiRequest(inbound.RawBody);
            var now = DateTime.UtcNow;

            var consent = request.ConsentId == null ? null : await _db.AbdmConsents.AsNoTracking().FirstOrDefaultAsync(
                c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hip && c.ConsentId == request.ConsentId, cancellationToken);

            var refusal = AbdmDataFlowRules.Refusal(consent, now);
            if (refusal == null && (request.TransactionId == null || request.PublicKey == null || request.Nonce == null || !AbdmDataFlowRules.IsSafePushUrl(request.DataPushUrl)))
                refusal = (AbdmDataFlowRules.InvalidRequestCode, "The request is missing its transaction id, key material or a usable data push URL.");

            var transfer = new AbdmDataTransfer
            {
                TransferId = Guid.NewGuid(),
                HospitalId = hospitalId,
                Role = AbdmConsentRole.Hip,
                ConsentId = request.ConsentId ?? string.Empty,
                TransactionId = request.TransactionId ?? string.Empty,
                Status = refusal == null ? AbdmTransferStatus.Requested : AbdmTransferStatus.Refused,
                ErrorMessage = refusal?.Message,
                RequestedAt = now
            };
            _db.AbdmDataTransfers.Add(transfer);
            await _db.SaveChangesAsync(cancellationToken);

            if (refusal != null)
            {
                await _hie.SendAsync(new AbdmHieCall
                {
                    Kind = "hi-on-request",
                    Path = OnRequestPath,
                    HospitalId = hospitalId,
                    SubjectRef = refusal.Value.Code,
                    ExpectsCallback = false,
                    Body = new
                    {
                        hiRequest = new { transactionId = request.TransactionId, sessionStatus = "ERRORED" },
                        error = new { code = refusal.Value.Code, message = refusal.Value.Message },
                        response = new { requestId = inbound.HeaderRequestId }
                    }
                }, cancellationToken);
                return;
            }

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "hi-on-request",
                Path = OnRequestPath,
                HospitalId = hospitalId,
                SubjectRef = request.TransactionId,
                ExpectsCallback = false,
                Body = new
                {
                    hiRequest = new { transactionId = request.TransactionId, sessionStatus = "ACKNOWLEDGED" },
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);

            await TransferAsync(transfer, consent!, request, cancellationToken);
        }

        private async Task TransferAsync(AbdmDataTransfer transfer, AbdmConsent consent, AbdmHiRequest request, CancellationToken cancellationToken)
        {
            var hospitalId = transfer.HospitalId;
            var references = AbdmDiscoveryRules.SplitReferences(consent.CareContextReferences);
            var hiTypes = AbdmDiscoveryRules.SplitReferences(consent.HiTypes);

            // Only care contexts of ours that are linked: the consent can name nothing else.
            var careContexts = await _db.AbdmCareContexts.AsNoTracking()
                .Where(c => c.HospitalId == hospitalId && c.LinkStatus == AbdmLinkStatus.Linked && references.Contains(c.ReferenceNumber))
                .ToListAsync(cancellationToken);

            var keys = AbdmFidelius.NewKeyMaterial();
            var entries = new List<object>();
            var statuses = new List<object>();
            try
            {
                foreach (var careContext in careContexts)
                {
                    var built = await _records.BuildAsync(careContext, hiTypes, cancellationToken);
                    if (built == null || !AbdmDataFlowRules.InRange(built.WhenUtc, consent, request))
                        continue;
                    foreach (var record in built.Records)
                    {
                        entries.Add(new
                        {
                            content = AbdmFidelius.Encrypt(record.Json, keys.PrivateKey, keys.Nonce, request.PublicKey!, request.Nonce!),
                            media = "application/fhir+json",
                            checksum = AbdmDataFlowRules.Md5Hex(record.Json),
                            careContextReference = careContext.ReferenceNumber
                        });
                    }
                    if (built.Records.Count > 0)
                        statuses.Add(new { careContextReference = careContext.ReferenceNumber, hiStatus = "DELIVERED", description = "Data sent successfully" });
                }

                var payload = JsonSerializer.Serialize(new
                {
                    pageNumber = 1,
                    pageCount = 1,
                    transactionId = request.TransactionId,
                    entries,
                    keyMaterial = new
                    {
                        cryptoAlg = AbdmFidelius.CryptoAlg,
                        curve = AbdmFidelius.Curve,
                        dhPublicKey = new
                        {
                            expiry = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                            parameters = AbdmFidelius.Parameters,
                            keyValue = keys.PublicKey
                        },
                        nonce = keys.Nonce
                    }
                }, JsonOptions);

                var status = await _push.PushAsync(request.DataPushUrl!, payload, cancellationToken);
                if (status < 200 || status >= 300)
                    throw new InvalidOperationException($"The HIU's data push URL answered HTTP {status}.");

                transfer.Status = AbdmTransferStatus.Transferred;
                transfer.RecordCount = entries.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The message may not carry patient data: only the exception type and our own text.
                _logger.LogWarning("ABDM data transfer {TransactionId} failed: {Error}.", request.TransactionId, ex.GetType().Name);
                transfer.Status = AbdmTransferStatus.Failed;
                transfer.ErrorMessage = ex is InvalidOperationException ? ex.Message : "The records could not be prepared or sent.";
                statuses = careContexts.Select(c => (object)new { careContextReference = c.ReferenceNumber, hiStatus = "ERRORED", description = "Data could not be sent" }).ToList();
            }
            transfer.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var hipId = await _db.AbdmFacilities.AsNoTracking().Where(f => f.HospitalId == hospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "hi-notify",
                Path = NotifyPath,
                HospitalId = hospitalId,
                SubjectRef = request.TransactionId,
                ExpectsCallback = false,
                Body = new
                {
                    notification = new
                    {
                        consentId = consent.ConsentId,
                        transactionId = request.TransactionId,
                        doneAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                        notifier = new { type = "HIP", id = hipId },
                        statusNotification = new
                        {
                            sessionStatus = transfer.Status == AbdmTransferStatus.Transferred ? "TRANSFERRED" : "FAILED",
                            hipId,
                            statusResponses = statuses
                        }
                    }
                }
            }, cancellationToken);
        }
    }
}
