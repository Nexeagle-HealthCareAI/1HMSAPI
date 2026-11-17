using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>A refusal to ask for consent, decided here before anything is sent to ABDM.</summary>
    public sealed class AbdmHiuRefusedException : InvalidOperationException
    {
        public AbdmHiuRefusedException(string message) : base(message) { }
    }

    public sealed class AbdmHiuConsentAsk
    {
        public Guid HospitalId { get; init; }
        public string PatientId { get; init; } = string.Empty;
        /// <summary>The patient's ABHA address; when empty, the one on their record is used.</summary>
        public string? AbhaAddress { get; init; }
        public string PurposeCode { get; init; } = "CAREMGT";
        public IReadOnlyList<string> HiTypes { get; init; } = Array.Empty<string>();
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        /// <summary>How long we may keep what we receive.</summary>
        public DateTime EraseAt { get; init; }
        public string? RequesterName { get; init; }
    }

    public interface IAbdmHiuService
    {
        /// <summary>Asks the patient, through ABDM, for consent to fetch their records from other facilities.</summary>
        Task<AbdmConsent> RequestConsentAsync(AbdmHiuConsentAsk ask, CancellationToken cancellationToken);
        Task OnConsentInitAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        Task OnConsentNotifyAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        Task OnConsentFetchedAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        Task OnDataRequestAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        /// <summary>A facility pushed encrypted records to the URL we gave for one transfer.
        /// False when the token names no transfer that is waiting.</summary>
        Task<bool> ReceiveDataAsync(string pushToken, string body, CancellationToken cancellationToken);
        /// <summary>Deletes records the consent no longer lets us keep. Returns how many went.</summary>
        Task<int> PurgeExpiredAsync(Guid hospitalId, CancellationToken cancellationToken);
        /// <summary>Whether this user works at this hospital. Records from elsewhere are shown to nobody else.</summary>
        Task<bool> CanAccessAsync(Guid callerUserId, Guid hospitalId, CancellationToken cancellationToken);
        Task<IReadOnlyList<AbdmConsent>> ListConsentsAsync(Guid hospitalId, string? patientId, CancellationToken cancellationToken);
        /// <summary>What we hold for a patient, newest first, without the record bodies. Expired records are removed first.</summary>
        Task<IReadOnlyList<AbdmReceivedRecordSummary>> ListRecordsAsync(Guid hospitalId, string patientId, CancellationToken cancellationToken);
        Task<AbdmReceivedRecord?> GetRecordAsync(Guid hospitalId, Guid receivedRecordId, CancellationToken cancellationToken);
    }

    public sealed record AbdmReceivedRecordSummary(Guid ReceivedRecordId, string ConsentId, string? CareContextReference, string? HiType, string? Title, DateTime ReceivedAt, DateTime? EraseAt, string? FromFacility);

    /// <summary>
    /// ABDM M3: this hospital as a Health Information User (abdm-m3 journey "Consent and data
    /// flow"). consent request init → on-init → the patient decides in their ABHA app → notify →
    /// on-notify + fetch → on-fetch → health-information request (with our one-time key) →
    /// on-request → the other facility pushes encrypted bundles to a URL made for that transfer →
    /// decrypt, store, and tell ABDM they arrived.
    /// What is received is kept only until the consent's dataEraseAt, and is removed at once when
    /// the patient revokes.
    /// </summary>
    public sealed class AbdmHiuService : IAbdmHiuService
    {
        private const string ConsentInitPath = "/api/hiecm/consent/v3/request/init";
        private const string ConsentOnNotifyPath = "/api/hiecm/consent/v3/request/hiu/on-notify";
        private const string ConsentFetchPath = "/api/hiecm/consent/v3/fetch";
        private const string DataRequestPath = "/api/hiecm/data-flow/v3/health-information/request";
        private const string DataNotifyPath = "/api/hiecm/data-flow/v3/health-information/notify";
        private const string PendingPrefix = "pending:";
        private const string Iso = "yyyy-MM-ddTHH:mm:ss.fffZ";

        /// <summary>Purposes of use ABDM defines, with the text sent alongside the code.</summary>
        public static readonly IReadOnlyDictionary<string, string> Purposes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CAREMGT"] = "Care Management",
            ["BTG"] = "Break the Glass",
            ["PUBHLTH"] = "Public Health",
            ["HPAYMT"] = "Healthcare Payment",
            ["DSRCH"] = "Disease Specific Healthcare Research",
            ["PATRQT"] = "Self Requested"
        };

        public static readonly IReadOnlyList<string> KnownHiTypes = new[]
        {
            "OPConsultation", "Prescription", "DiagnosticReport", "DischargeSummary", "ImmunizationRecord", "HealthDocumentRecord", "WellnessRecord"
        };

        private readonly AppDbContext _db;
        private readonly IAbdmHieService _hie;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AbdmHiuService> _logger;

        public AbdmHiuService(AppDbContext db, IAbdmHieService hie, IConfiguration configuration, ILogger<AbdmHiuService> logger)
        {
            _db = db;
            _hie = hie;
            _configuration = configuration;
            _logger = logger;
        }

        // ---- 1. ask for consent --------------------------------------------------------------

        public async Task<AbdmConsent> RequestConsentAsync(AbdmHiuConsentAsk ask, CancellationToken cancellationToken)
        {
            if (!Purposes.TryGetValue(ask.PurposeCode ?? string.Empty, out var purposeText))
                throw new AbdmHiuRefusedException("Choose a purpose for the request.");
            var hiTypes = KnownHiTypes.Where(t => ask.HiTypes.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            if (hiTypes.Count == 0)
                throw new AbdmHiuRefusedException("Choose at least one kind of record to ask for.");
            if (ask.From >= ask.To)
                throw new AbdmHiuRefusedException("The 'from' date must be before the 'to' date.");
            if (ask.EraseAt <= DateTime.UtcNow)
                throw new AbdmHiuRefusedException("The date to keep the records until must be in the future.");

            var patient = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == ask.HospitalId && p.PatientId == ask.PatientId)
                .Select(p => new { p.AbhaId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new AbdmHiuRefusedException("The patient was not found in this hospital.");

            var abhaAddress = ask.AbhaAddress?.Trim();
            if (string.IsNullOrEmpty(abhaAddress) && !string.IsNullOrWhiteSpace(patient.AbhaId))
            {
                if (patient.AbhaId.Contains('@'))
                    abhaAddress = patient.AbhaId.Trim();
                else
                {
                    var digits = AbdmCareContextRules.Digits(patient.AbhaId);
                    var accounts = await _db.AbhaAccount.AsNoTracking()
                        .Where(a => a.HospitalId == ask.HospitalId && a.AbhaAddress != null)
                        .Select(a => new { a.AbhaNumber, a.AbhaAddress, a.CreatedAt })
                        .ToListAsync(cancellationToken);
                    abhaAddress = accounts.Where(a => AbdmCareContextRules.Digits(a.AbhaNumber) == digits)
                        .OrderByDescending(a => a.CreatedAt).Select(a => a.AbhaAddress).FirstOrDefault();
                }
            }
            if (string.IsNullOrEmpty(abhaAddress) || !abhaAddress.Contains('@'))
                throw new AbdmHiuRefusedException("ABDM needs the patient's ABHA address (like name@abdm) to ask for consent. Verify their ABHA first.");

            var hiuId = await _db.AbdmFacilities.AsNoTracking().Where(f => f.HospitalId == ask.HospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var consent = new AbdmConsent
            {
                ConsentRowId = Guid.NewGuid(),
                HospitalId = ask.HospitalId,
                Role = AbdmConsentRole.Hiu,
                Status = AbdmConsentStatus.Requested,
                PatientAbhaAddress = abhaAddress,
                PatientId = ask.PatientId,
                PurposeCode = ask.PurposeCode!.ToUpperInvariant(),
                PurposeText = purposeText,
                HiTypes = string.Join(',', hiTypes),
                DateFrom = ask.From,
                DateTo = ask.To,
                ExpiresAt = ask.EraseAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            // The row exists before the call goes out, so on-init always finds it.
            consent.ConsentId = PendingPrefix + consent.ConsentRowId;
            _db.AbdmConsents.Add(consent);
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                await _hie.SendAsync(new AbdmHieCall
                {
                    Kind = "consent-init",
                    Path = ConsentInitPath,
                    HospitalId = ask.HospitalId,
                    SubjectRef = consent.ConsentRowId.ToString(),
                    ExpectsCallback = true,
                    FacilityHeader = AbdmFacilityHeader.Hiu,
                    Body = new
                    {
                        consent = new
                        {
                            purpose = new { text = purposeText, code = consent.PurposeCode, refUri = "http://terminology.hl7.org/ValueSet/v3-PurposeOfUse" },
                            patient = new { id = abhaAddress },
                            hiu = new { id = hiuId },
                            requester = new { name = string.IsNullOrWhiteSpace(ask.RequesterName) ? "Hospital staff" : ask.RequesterName!.Trim() },
                            hiTypes,
                            permission = new
                            {
                                accessMode = "VIEW",
                                dateRange = new { from = Stamp(ask.From), to = Stamp(ask.To) },
                                dataEraseAt = Stamp(ask.EraseAt),
                                frequency = new { unit = "HOUR", value = 1, repeats = 0 }
                            }
                        }
                    }
                }, cancellationToken);
            }
            catch (AbdmException ex)
            {
                consent.Status = AbdmConsentStatus.Failed;
                consent.CounterpartyName = Short(ex.Message);
                consent.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return consent;
        }

        public async Task OnConsentInitAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            var exchange = await _db.AbdmExchanges.AsNoTracking().FirstOrDefaultAsync(e => e.RequestId == inbound.AnswersRequestId, cancellationToken);
            if (exchange?.HospitalId == null || exchange.SubjectRef == null)
                return;
            var pendingId = PendingPrefix + exchange.SubjectRef;
            var consent = await _db.AbdmConsents.FirstOrDefaultAsync(
                c => c.HospitalId == exchange.HospitalId && c.Role == AbdmConsentRole.Hiu && c.ConsentId == pendingId, cancellationToken);
            if (consent == null)
                return;

            var requestId = AbdmDataFlowRules.FindIn(inbound.RawBody, "consentRequest", "id");
            if (exchange.ErrorCode != null || requestId == null)
            {
                consent.Status = AbdmConsentStatus.Failed;
                consent.CounterpartyName = Short(exchange.ErrorMessage ?? "ABDM did not accept the consent request.");
            }
            else
            {
                consent.ConsentRequestId = requestId;
                consent.ConsentId = "request:" + requestId;
            }
            consent.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ---- 2. the patient decided ----------------------------------------------------------

        public async Task OnConsentNotifyAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var requestId = AbdmCareContextRules.FindString(inbound.RawBody, "consentRequestId");
            var status = (AbdmCareContextRules.FindString(inbound.RawBody, "status") ?? string.Empty).Trim().ToUpperInvariant();
            var artefactIds = ArtefactIds(inbound.RawBody);

            var rows = requestId == null ? new List<AbdmConsent>() : await _db.AbdmConsents
                .Where(c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hiu && c.ConsentRequestId == requestId)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;

            if (rows.Count > 0 && status == AbdmConsentStatus.Granted && artefactIds.Count > 0)
            {
                // One request can be granted as several artefacts (one per facility holding records).
                var template = rows[0];
                foreach (var artefactId in artefactIds)
                {
                    var row = rows.FirstOrDefault(r => r.ConsentId == artefactId) ?? rows.FirstOrDefault(r => r.ConsentId.StartsWith("request:", StringComparison.Ordinal));
                    if (row == null)
                    {
                        row = new AbdmConsent
                        {
                            ConsentRowId = Guid.NewGuid(), HospitalId = hospitalId, Role = AbdmConsentRole.Hiu, ConsentRequestId = requestId,
                            PatientAbhaAddress = template.PatientAbhaAddress, PatientId = template.PatientId, PurposeCode = template.PurposeCode,
                            PurposeText = template.PurposeText, HiTypes = template.HiTypes, DateFrom = template.DateFrom, DateTo = template.DateTo,
                            ExpiresAt = template.ExpiresAt, CreatedAt = now
                        };
                        _db.AbdmConsents.Add(row);
                        rows.Add(row);
                    }
                    row.ConsentId = artefactId;
                    row.Status = AbdmConsentStatus.Granted;
                    row.UpdatedAt = now;
                }
            }
            else if (rows.Count > 0 && status.Length > 0)
            {
                // Denied, revoked or expired. A notice that names artefacts applies to those only.
                var affected = artefactIds.Count > 0 ? rows.Where(r => artefactIds.Contains(r.ConsentId)).ToList() : rows;
                foreach (var row in affected)
                {
                    row.Status = status;
                    row.UpdatedAt = now;
                }
                if (status is AbdmConsentStatus.Revoked or AbdmConsentStatus.Expired)
                {
                    var gone = affected.Select(r => r.ConsentId).ToList();
                    var records = await _db.AbdmReceivedRecords.Where(r => r.HospitalId == hospitalId && gone.Contains(r.ConsentId)).ToListAsync(cancellationToken);
                    _db.AbdmReceivedRecords.RemoveRange(records);
                }
            }
            await _db.SaveChangesAsync(cancellationToken);

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "hiu-consent-on-notify",
                Path = ConsentOnNotifyPath,
                HospitalId = hospitalId,
                SubjectRef = requestId,
                ExpectsCallback = false,
                FacilityHeader = AbdmFacilityHeader.Hiu,
                Body = new
                {
                    acknowledgement = artefactIds.Select(id => new { status = "OK", consentId = id }).ToArray(),
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);

            if (rows.Count == 0 || status != AbdmConsentStatus.Granted)
                return;
            foreach (var artefactId in artefactIds)
            {
                try
                {
                    await _hie.SendAsync(new AbdmHieCall
                    {
                        Kind = "consent-fetch",
                        Path = ConsentFetchPath,
                        HospitalId = hospitalId,
                        SubjectRef = artefactId,
                        ExpectsCallback = true,
                        FacilityHeader = AbdmFacilityHeader.Hiu,
                        Body = new { consentId = artefactId }
                    }, cancellationToken);
                }
                catch (AbdmException ex)
                {
                    _logger.LogWarning("ABDM consent fetch for {ConsentId} was refused: {Code}.", artefactId, ex.Code ?? "none");
                }
            }
        }

        // ---- 3. the artefact, then the request for records ------------------------------------

        public async Task OnConsentFetchedAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            var exchange = await _db.AbdmExchanges.AsNoTracking().FirstOrDefaultAsync(e => e.RequestId == inbound.AnswersRequestId, cancellationToken);
            if (exchange?.HospitalId == null || exchange.SubjectRef == null || exchange.ErrorCode != null)
                return;
            var hospitalId = exchange.HospitalId.Value;
            var body = inbound.RawBody;
            var consent = await _db.AbdmConsents.FirstOrDefaultAsync(
                c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hiu && c.ConsentId == exchange.SubjectRef, cancellationToken);
            if (consent == null)
                return;

            consent.CounterpartyId = AbdmDataFlowRules.FindIn(body, "hip", "id") ?? consent.CounterpartyId;
            consent.CounterpartyName = AbdmDataFlowRules.FindIn(body, "hip", "name") ?? consent.CounterpartyName;
            var hiTypes = AbdmDataFlowRules.Strings(body, "hiTypes");
            if (hiTypes.Count > 0)
                consent.HiTypes = string.Join(',', hiTypes);
            consent.CareContextReferences = string.Join(',', AbdmDataFlowRules.AllStrings(body, "careContextReference"));
            consent.DateFrom = AbdmDataFlowRules.Date(AbdmDataFlowRules.FindIn(body, "dateRange", "from")) ?? consent.DateFrom;
            consent.DateTo = AbdmDataFlowRules.Date(AbdmDataFlowRules.FindIn(body, "dateRange", "to")) ?? consent.DateTo;
            consent.ExpiresAt = AbdmDataFlowRules.Date(AbdmCareContextRules.FindString(body, "dataEraseAt")) ?? consent.ExpiresAt;
            var status = AbdmDataFlowRules.FindIn(body, "consent", "status");
            if (status != null)
                consent.Status = status.ToUpperInvariant();
            consent.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            if (AbdmDataFlowRules.Refusal(consent, DateTime.UtcNow) == null)
                await RequestDataAsync(consent, cancellationToken);
        }

        private async Task RequestDataAsync(AbdmConsent consent, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var transfer = new AbdmDataTransfer
            {
                TransferId = Guid.NewGuid(),
                HospitalId = consent.HospitalId,
                Role = AbdmConsentRole.Hiu,
                ConsentId = consent.ConsentId,
                Status = AbdmTransferStatus.Requested,
                RequestedAt = now
            };
            transfer.TransactionId = PendingPrefix + transfer.TransferId;

            var publicBase = _configuration["Abdm:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(publicBase) || publicBase.StartsWith('<'))
            {
                transfer.Status = AbdmTransferStatus.Failed;
                transfer.ErrorMessage = "Records were not requested: this server has no public address configured (Abdm:PublicBaseUrl) for the other facility to send them to.";
                transfer.CompletedAt = now;
                _db.AbdmDataTransfers.Add(transfer);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var keys = AbdmFidelius.NewKeyMaterial();
            transfer.PrivateKey = keys.PrivateKey;
            transfer.Nonce = keys.Nonce;
            transfer.PushToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _db.AbdmDataTransfers.Add(transfer);
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                await _hie.SendAsync(new AbdmHieCall
                {
                    Kind = "hi-request",
                    Path = DataRequestPath,
                    HospitalId = consent.HospitalId,
                    SubjectRef = transfer.TransferId.ToString(),
                    ExpectsCallback = true,
                    FacilityHeader = AbdmFacilityHeader.Hiu,
                    Body = new
                    {
                        hiRequest = new
                        {
                            consent = new { id = consent.ConsentId },
                            dateRange = new { from = Stamp(consent.DateFrom ?? now.AddYears(-10)), to = Stamp(consent.DateTo ?? now) },
                            dataPushUrl = $"{publicBase.TrimEnd('/')}/abdm-hiu/data-push/{transfer.PushToken}",
                            keyMaterial = new
                            {
                                cryptoAlg = AbdmFidelius.CryptoAlg,
                                curve = "curve25519",
                                dhPublicKey = new { expiry = Stamp(now.AddDays(1)), parameters = "Ephemeral public key", keyValue = keys.PublicKey },
                                nonce = keys.Nonce
                            }
                        }
                    }
                }, cancellationToken);
            }
            catch (AbdmException ex)
            {
                await FailAsync(transfer, ex.Message, cancellationToken);
            }
        }

        public async Task OnDataRequestAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            var exchange = await _db.AbdmExchanges.AsNoTracking().FirstOrDefaultAsync(e => e.RequestId == inbound.AnswersRequestId, cancellationToken);
            if (exchange?.HospitalId == null || !Guid.TryParse(exchange.SubjectRef, out var transferId))
                return;
            var transfer = await _db.AbdmDataTransfers.FirstOrDefaultAsync(t => t.TransferId == transferId && t.Role == AbdmConsentRole.Hiu, cancellationToken);
            if (transfer == null || transfer.Status != AbdmTransferStatus.Requested)
                return;

            var transactionId = AbdmDataFlowRules.FindIn(inbound.RawBody, "hiRequest", "transactionId");
            if (exchange.ErrorCode != null || transactionId == null)
            {
                await FailAsync(transfer, exchange.ErrorMessage ?? "ABDM did not accept the request for records.", cancellationToken);
                return;
            }
            transfer.TransactionId = transactionId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ---- 4. the records arrive -----------------------------------------------------------

        public async Task<bool> ReceiveDataAsync(string pushToken, string body, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pushToken))
                return false;
            var transfer = await _db.AbdmDataTransfers.FirstOrDefaultAsync(
                t => t.PushToken == pushToken && t.Role == AbdmConsentRole.Hiu && t.Status == AbdmTransferStatus.Requested, cancellationToken);
            if (transfer == null || transfer.PrivateKey == null || transfer.Nonce == null)
                return false;

            var consent = await _db.AbdmConsents.AsNoTracking().FirstOrDefaultAsync(
                c => c.HospitalId == transfer.HospitalId && c.Role == AbdmConsentRole.Hiu && c.ConsentId == transfer.ConsentId, cancellationToken);
            if (AbdmDataFlowRules.Refusal(consent, DateTime.UtcNow) != null)
            {
                // Revoked or expired while the transfer was on its way: take nothing.
                await FailAsync(transfer, "The consent was no longer valid when the records arrived.", cancellationToken);
                return true;
            }

            var page = ParsePush(body);
            var now = DateTime.UtcNow;
            var statuses = new List<object>();
            var stored = 0;
            foreach (var entry in page.Entries)
            {
                try
                {
                    if (entry.Content == null || page.PublicKey == null || page.Nonce == null)
                        throw new CryptographicException("The entry carries no inline content or the page carries no key material.");
                    var bundle = AbdmFidelius.Decrypt(entry.Content, transfer.PrivateKey, transfer.Nonce, page.PublicKey, page.Nonce);
                    var (title, hiType) = Describe(bundle);
                    _db.AbdmReceivedRecords.Add(new AbdmReceivedRecord
                    {
                        ReceivedRecordId = Guid.NewGuid(),
                        HospitalId = transfer.HospitalId,
                        ConsentId = transfer.ConsentId,
                        TransactionId = transfer.TransactionId,
                        PatientId = consent!.PatientId,
                        CareContextReference = entry.CareContextReference,
                        HiType = hiType,
                        Title = title,
                        BundleJson = bundle,
                        ReceivedAt = now,
                        EraseAt = consent.ExpiresAt
                    });
                    stored++;
                    statuses.Add(new { careContextReference = entry.CareContextReference, hiStatus = "OK", description = "Data received successfully" });
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
                {
                    _logger.LogWarning("ABDM transfer {TransactionId}: one entry could not be read ({Error}).", transfer.TransactionId, ex.GetType().Name);
                    statuses.Add(new { careContextReference = entry.CareContextReference, hiStatus = "ERRORED", description = "Data could not be read" });
                }
            }

            transfer.RecordCount += stored;
            var lastPage = page.PageNumber >= page.PageCount;
            if (lastPage)
            {
                transfer.Status = stored > 0 || page.Entries.Count == 0 ? AbdmTransferStatus.Transferred : AbdmTransferStatus.Failed;
                if (transfer.Status == AbdmTransferStatus.Failed)
                    transfer.ErrorMessage = "The records arrived but could not be decrypted.";
                transfer.CompletedAt = now;
                // The key has done its job; without it nothing more can be read under this transfer.
                transfer.PrivateKey = null;
                transfer.Nonce = null;
                transfer.PushToken = null;
            }
            await _db.SaveChangesAsync(cancellationToken);

            if (lastPage)
            {
                var hiuId = await _db.AbdmFacilities.AsNoTracking().Where(f => f.HospitalId == transfer.HospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
                try
                {
                    await _hie.SendAsync(new AbdmHieCall
                    {
                        Kind = "hiu-hi-notify",
                        Path = DataNotifyPath,
                        HospitalId = transfer.HospitalId,
                        SubjectRef = transfer.TransactionId,
                        ExpectsCallback = false,
                        FacilityHeader = AbdmFacilityHeader.Hiu,
                        Body = new
                        {
                            notification = new
                            {
                                consentId = transfer.ConsentId,
                                transactionId = transfer.TransactionId,
                                doneAt = Stamp(now),
                                notifier = new { type = "HIU", id = hiuId },
                                statusNotification = new
                                {
                                    // The value NHA's own example gives for an HIU that has the data.
                                    sessionStatus = transfer.Status == AbdmTransferStatus.Transferred ? "RECEIVED" : "FAILED",
                                    hipId = consent!.CounterpartyId,
                                    statusResponses = statuses
                                }
                            }
                        }
                    }, cancellationToken);
                }
                catch (AbdmException ex)
                {
                    _logger.LogWarning("ABDM was not told that transfer {TransactionId} arrived: {Code}.", transfer.TransactionId, ex.Code ?? "none");
                }
            }
            return true;
        }

        public async Task<int> PurgeExpiredAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var expired = await _db.AbdmReceivedRecords.Where(r => r.HospitalId == hospitalId && r.EraseAt != null && r.EraseAt < now).ToListAsync(cancellationToken);
            if (expired.Count == 0)
                return 0;
            _db.AbdmReceivedRecords.RemoveRange(expired);
            await _db.SaveChangesAsync(cancellationToken);
            return expired.Count;
        }

        // ---- reading what we hold -------------------------------------------------------------

        public Task<bool> CanAccessAsync(Guid callerUserId, Guid hospitalId, CancellationToken cancellationToken) =>
            EasyHMSAPI.Application.Common.CallerGuards.IsHospitalMemberAsync(_db, callerUserId, hospitalId, cancellationToken);

        public async Task<IReadOnlyList<AbdmConsent>> ListConsentsAsync(Guid hospitalId, string? patientId, CancellationToken cancellationToken)
        {
            var query = _db.AbdmConsents.AsNoTracking().Where(c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hiu);
            if (!string.IsNullOrWhiteSpace(patientId))
                query = query.Where(c => c.PatientId == patientId);
            return await query.OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AbdmReceivedRecordSummary>> ListRecordsAsync(Guid hospitalId, string patientId, CancellationToken cancellationToken)
        {
            await PurgeExpiredAsync(hospitalId, cancellationToken);
            var rows = await _db.AbdmReceivedRecords.AsNoTracking()
                .Where(r => r.HospitalId == hospitalId && r.PatientId == patientId)
                .OrderByDescending(r => r.ReceivedAt)
                .Select(r => new { r.ReceivedRecordId, r.ConsentId, r.CareContextReference, r.HiType, r.Title, r.ReceivedAt, r.EraseAt })
                .Take(500)
                .ToListAsync(cancellationToken);
            var consentIds = rows.Select(r => r.ConsentId).Distinct().ToList();
            var facilities = await _db.AbdmConsents.AsNoTracking()
                .Where(c => c.HospitalId == hospitalId && c.Role == AbdmConsentRole.Hiu && consentIds.Contains(c.ConsentId))
                .Select(c => new { c.ConsentId, Name = c.CounterpartyName ?? c.CounterpartyId })
                .ToListAsync(cancellationToken);
            return rows.Select(r => new AbdmReceivedRecordSummary(r.ReceivedRecordId, r.ConsentId, r.CareContextReference, r.HiType, r.Title, r.ReceivedAt, r.EraseAt,
                facilities.FirstOrDefault(f => f.ConsentId == r.ConsentId)?.Name)).ToList();
        }

        public async Task<AbdmReceivedRecord?> GetRecordAsync(Guid hospitalId, Guid receivedRecordId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            return await _db.AbdmReceivedRecords.AsNoTracking().FirstOrDefaultAsync(
                r => r.HospitalId == hospitalId && r.ReceivedRecordId == receivedRecordId && (r.EraseAt == null || r.EraseAt > now), cancellationToken);
        }

        // ---- helpers -------------------------------------------------------------------------

        private async Task FailAsync(AbdmDataTransfer transfer, string message, CancellationToken cancellationToken)
        {
            transfer.Status = AbdmTransferStatus.Failed;
            transfer.ErrorMessage = Short(message);
            transfer.CompletedAt = DateTime.UtcNow;
            transfer.PrivateKey = null;
            transfer.Nonce = null;
            transfer.PushToken = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string Stamp(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(Iso, CultureInfo.InvariantCulture);

        private static string Short(string message) => message.Length > 200 ? message[..200] : message;

        /// <summary>The ids under notification.consentArtefacts.</summary>
        public static IReadOnlyList<string> ArtefactIds(string? body)
        {
            var ids = new List<string>();
            if (string.IsNullOrWhiteSpace(body))
                return ids;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("notification", out var notification) && notification.ValueKind == JsonValueKind.Object
                    && notification.TryGetProperty("consentArtefacts", out var artefacts) && artefacts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var artefact in artefacts.EnumerateArray())
                    {
                        if (artefact.ValueKind == JsonValueKind.Object && artefact.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(id.GetString()))
                            ids.Add(id.GetString()!.Trim());
                    }
                }
            }
            catch (JsonException)
            {
                // Unreadable: no artefacts.
            }
            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private sealed record PushEntry(string? Content, string? CareContextReference);
        private sealed record PushPage(int PageNumber, int PageCount, string? PublicKey, string? Nonce, IReadOnlyList<PushEntry> Entries);

        private static PushPage ParsePush(string body)
        {
            var entries = new List<PushEntry>();
            int pageNumber = 1, pageCount = 1;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("pageNumber", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var n)) pageNumber = n;
                    if (root.TryGetProperty("pageCount", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var c)) pageCount = c;
                    if (root.TryGetProperty("entries", out var array) && array.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in array.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            entries.Add(new PushEntry(
                                item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() : null,
                                item.TryGetProperty("careContextReference", out var reference) && reference.ValueKind == JsonValueKind.String ? reference.GetString() : null));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Unreadable page: no entries.
            }
            return new PushPage(pageNumber, pageCount, AbdmCareContextRules.FindString(body, "keyValue"), AbdmCareContextRules.FindString(body, "nonce"), entries);
        }

        /// <summary>The title and record type of a received bundle, from its Composition.</summary>
        public static (string? Title, string? HiType) Describe(string bundleJson)
        {
            using var doc = JsonDocument.Parse(bundleJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return (null, null);
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                    continue;
                if (!resource.TryGetProperty("resourceType", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "Composition")
                    continue;
                var title = resource.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                string? hiType = null;
                if (resource.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object
                    && meta.TryGetProperty("profile", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var profile in profiles.EnumerateArray())
                    {
                        if (profile.ValueKind != JsonValueKind.String) continue;
                        var name = (profile.GetString() ?? string.Empty).Split('/').Last();
                        hiType = KnownHiTypes.FirstOrDefault(k => name.StartsWith(k, StringComparison.OrdinalIgnoreCase)) ?? hiType;
                    }
                }
                return (title != null && title.Length > 200 ? title[..200] : title, hiType);
            }
            return (null, null);
        }
    }
}
