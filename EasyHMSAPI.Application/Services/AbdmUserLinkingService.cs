using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    public interface IAbdmUserLinkingService
    {
        /// <summary>A patient is searching for their records: answer with on-discover.</summary>
        Task OnDiscoverAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        /// <summary>The patient chose records to link: send them an OTP and answer with on-init.</summary>
        Task OnInitAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        /// <summary>The patient typed the OTP: check it, link, and answer with on-confirm.</summary>
        Task OnConfirmAsync(AbdmInbound inbound, CancellationToken cancellationToken);
    }

    /// <summary>
    /// User-initiated linking (abdm-m2 journey "User initiated linking"): discover → on-discover,
    /// init → on-init, confirm → on-confirm. ABDM asks on our bridge URL; every answer is a new
    /// call to ABDM that quotes the REQUEST-ID of the question in response.requestId. An answer is
    /// always sent, including "nothing found" — silence shows in the patient's app as a hospital
    /// that is down.
    /// </summary>
    public sealed class AbdmUserLinkingService : IAbdmUserLinkingService
    {
        private const string OnDiscoverPath = "/api/hiecm/user-initiated-linking/v3/patient/care-context/on-discover";
        private const string OnInitPath = "/api/hiecm/user-initiated-linking/v3/link/care-context/on-init";
        private const string OnConfirmPath = "/api/hiecm/user-initiated-linking/v3/link/care-context/on-confirm";

        // Error codes from ABDM's M2 error page (read 2026-11-18). ABDM does not say which code
        // each refusal should carry, so these are the closest by their published message:
        // 1001 "No data found", 1012 "No records found against the ABHA Address", 1017 "Invalid
        // TransactionId", 1057 "Invalid Care Contexts", 1056 "Invalid Link Reference Number",
        // 1036 "Data does not matched", 1004 "SMS Gateway is unavailable".
        public const string NoPatientCode = "ABDM-1001";
        public const string NoRecordsCode = "ABDM-1012";
        public const string InvalidTransactionCode = "ABDM-1017";
        public const string InvalidCareContextsCode = "ABDM-1057";
        public const string InvalidLinkReferenceCode = "ABDM-1056";
        public const string OtpFailedCode = "ABDM-1036";
        public const string OtpNotSentCode = "ABDM-1004";

        private readonly AppDbContext _db;
        private readonly IAbdmHieService _hie;
        private readonly IAbdmLinkingService _linking;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly ISmsService? _sms;
        private readonly ILogger<AbdmUserLinkingService> _logger;

        public AbdmUserLinkingService(AppDbContext db, IAbdmHieService hie, IAbdmLinkingService linking, IWhatsAppMessagingService whatsApp, ILogger<AbdmUserLinkingService> logger, ISmsService? sms = null)
        {
            _db = db;
            _hie = hie;
            _linking = linking;
            _whatsApp = whatsApp;
            _sms = sms;
            _logger = logger;
        }

        // ---- discover ------------------------------------------------------------------------

        public async Task OnDiscoverAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var request = AbdmDiscoveryRules.Parse(inbound.RawBody);

            var match = AbdmDiscoveryRules.Match(request, await CandidatesAsync(hospitalId, request, cancellationToken));
            if (match == null)
            {
                await ReplyErrorAsync("on-discover", OnDiscoverPath, inbound, request.TransactionId, NoPatientCode, "No patient at this facility matches the details given.", cancellationToken);
                return;
            }

            var offered = await _linking.UnlinkedForPatientAsync(hospitalId, match.Patient.PatientId, cancellationToken);
            if (offered.Count == 0)
            {
                await ReplyErrorAsync("on-discover", OnDiscoverPath, inbound, request.TransactionId, NoRecordsCode, "This patient has no unlinked records at this facility.", cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            _db.AbdmLinkRequests.Add(new AbdmLinkRequest
            {
                LinkRequestId = Guid.NewGuid(),
                HospitalId = hospitalId,
                TransactionId = request.TransactionId ?? string.Empty,
                PatientId = match.Patient.PatientId,
                AbhaAddress = request.AbhaAddress,
                OfferedReferences = string.Join(',', offered.Select(c => c.ReferenceNumber)),
                Status = AbdmLinkRequestStatus.Discovered,
                CreatedAt = now,
                UpdatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "on-discover",
                Path = OnDiscoverPath,
                HospitalId = hospitalId,
                // ABDM's page for on-discover names X-HIU-ID as the facility header, unlike every
                // other HIP call, so this one answer carries the facility id under both names.
                FacilityHeader = AbdmFacilityHeader.HipAndHiu,
                SubjectRef = request.AbhaAddress ?? match.Patient.PatientId,
                ExpectsCallback = false,
                Body = new
                {
                    transactionId = request.TransactionId,
                    patient = PatientEntries(match.Patient.PatientId, match.Patient.FullName, offered),
                    matchedBy = match.MatchedBy,
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);
        }

        /// <summary>Only patients who could match are loaded: by ABHA as stored, or by the verified mobile.</summary>
        private async Task<List<AbdmDiscoveryCandidate>> CandidatesAsync(Guid hospitalId, AbdmDiscoverRequest request, CancellationToken cancellationToken)
        {
            var abhaSpellings = AbdmDiscoveryRules.AbhaNumberVariants(request.VerifiedValue("ABHA_NUMBER")).ToList();
            if (!string.IsNullOrWhiteSpace(request.AbhaAddress))
                abhaSpellings.Add(request.AbhaAddress.Trim());
            var mobile = AbdmDiscoveryRules.Mobile10(request.VerifiedValue("MOBILE"));
            if (abhaSpellings.Count == 0 && mobile == null)
                return new List<AbdmDiscoveryCandidate>();

            var rows = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.PatientId != null && p.MergedIntoPatientId == null
                    && ((p.AbhaId != null && abhaSpellings.Contains(p.AbhaId))
                        || (mobile != null && p.Mobile != null && p.Mobile.EndsWith(mobile!))))
                .Select(p => new { p.PatientId, p.FullName, p.Mobile, p.Sex, p.DateOfBirth, p.Age, p.AgeUnit, p.AbhaId, p.RegisteredAt })
                .Take(50)
                .ToListAsync(cancellationToken);

            return rows.Select(p =>
            {
                var (year, exact) = AbdmDiscoveryRules.YearOfBirth(p.DateOfBirth, p.Age, p.AgeUnit, p.RegisteredAt);
                return new AbdmDiscoveryCandidate(p.PatientId!, p.FullName, p.Mobile, p.Sex, year, exact, p.AbhaId, p.RegisteredAt);
            }).ToList();
        }

        // ---- init ----------------------------------------------------------------------------

        public async Task OnInitAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var transactionId = AbdmCareContextRules.FindString(inbound.RawBody, "transactionId");

            var linkRequest = transactionId == null ? null : await _db.AbdmLinkRequests
                .Where(r => r.HospitalId == hospitalId && r.TransactionId == transactionId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (linkRequest == null || linkRequest.Status is AbdmLinkRequestStatus.Linked or AbdmLinkRequestStatus.Closed)
            {
                await ReplyErrorAsync("on-init", OnInitPath, inbound, transactionId, InvalidTransactionCode, "This linking request was not found. Search for the records again.", cancellationToken);
                return;
            }

            // Only what this discovery offered this patient can be asked for.
            var offered = AbdmDiscoveryRules.SplitReferences(linkRequest.OfferedReferences);
            var requested = AbdmDiscoveryRules.RequestedReferences(inbound.RawBody)
                .Where(r => offered.Contains(r, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (requested.Count == 0)
            {
                await ReplyErrorAsync("on-init", OnInitPath, inbound, transactionId, InvalidCareContextsCode, "None of the records asked for were offered to this patient.", cancellationToken);
                return;
            }

            var mobile = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.PatientId == linkRequest.PatientId)
                .Select(p => p.Mobile)
                .FirstOrDefaultAsync(cancellationToken);

            var otp = AbdmDiscoveryRules.NewOtp();
            var linkReference = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.Add(AbdmDiscoveryRules.OtpLifetime);

            var sent = false;
            if (AbdmDiscoveryRules.Mobile10(mobile) != null)
            {
                try
                {
                    sent = await _whatsApp.SendOtpAsync(mobile!, otp);
                }
                catch (Exception ex)
                {
                    // The OTP never appears in a log line.
                    _logger.LogWarning(ex, "ABDM linking OTP could not be sent by WhatsApp for transaction {TransactionId}.", transactionId);
                }
                // A mobile with no WhatsApp still has to get the OTP (USER_INIT_LINK_605): fall back to SMS.
                if (!sent && _sms != null)
                {
                    try
                    {
                        sent = await _sms.SendOtpSmsAsync(mobile!, otp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ABDM linking OTP could not be sent by SMS for transaction {TransactionId}.", transactionId);
                    }
                }
            }
            if (!sent)
            {
                await ReplyErrorAsync("on-init", OnInitPath, inbound, transactionId, OtpNotSentCode, "The OTP could not be sent to the mobile number this facility has for the patient.", cancellationToken);
                return;
            }

            linkRequest.RequestedReferences = string.Join(',', requested);
            linkRequest.LinkReferenceNumber = linkReference;
            linkRequest.OtpHash = AbdmDiscoveryRules.OtpHash(otp, linkReference);
            linkRequest.OtpExpiresAt = expiresAt;
            linkRequest.OtpAttempts = 0;
            linkRequest.Status = AbdmLinkRequestStatus.OtpSent;
            linkRequest.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "on-init",
                Path = OnInitPath,
                HospitalId = hospitalId,
                SubjectRef = linkRequest.AbhaAddress ?? linkRequest.PatientId,
                ExpectsCallback = false,
                Body = new
                {
                    transactionId,
                    link = new
                    {
                        referenceNumber = linkReference,
                        authenticationType = "DIRECT",
                        meta = new
                        {
                            communicationMedium = "MOBILE",
                            communicationHint = AbdmDiscoveryRules.MaskedMobile(mobile),
                            communicationExpiry = expiresAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)
                        }
                    },
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);
        }

        // ---- confirm -------------------------------------------------------------------------

        public async Task OnConfirmAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            if (inbound.HospitalId == null)
                return;
            var hospitalId = inbound.HospitalId.Value;
            var linkReference = AbdmCareContextRules.FindString(inbound.RawBody, "linkRefNumber");
            var token = AbdmDiscoveryRules.ConfirmToken(inbound.RawBody);

            var linkRequest = linkReference == null ? null : await _db.AbdmLinkRequests
                .FirstOrDefaultAsync(r => r.HospitalId == hospitalId && r.LinkReferenceNumber == linkReference, cancellationToken);
            if (linkRequest == null || linkRequest.Status != AbdmLinkRequestStatus.OtpSent)
            {
                await ReplyErrorAsync("on-confirm", OnConfirmPath, inbound, null, InvalidLinkReferenceCode, "This link reference is not valid. Start the linking again.", cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            if (linkRequest.OtpExpiresAt == null || linkRequest.OtpExpiresAt.Value < now)
            {
                linkRequest.Status = AbdmLinkRequestStatus.Closed;
                linkRequest.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await ReplyErrorAsync("on-confirm", OnConfirmPath, inbound, null, OtpFailedCode, "The OTP has expired. Start the linking again.", cancellationToken);
                return;
            }

            if (!AbdmDiscoveryRules.OtpMatches(token, linkReference!, linkRequest.OtpHash))
            {
                linkRequest.OtpAttempts++;
                if (linkRequest.OtpAttempts >= AbdmDiscoveryRules.OtpMaxAttempts)
                    linkRequest.Status = AbdmLinkRequestStatus.Closed;
                linkRequest.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await ReplyErrorAsync("on-confirm", OnConfirmPath, inbound, null, OtpFailedCode,
                    linkRequest.Status == AbdmLinkRequestStatus.Closed ? "The OTP was wrong too many times. Start the linking again." : "The OTP does not match.", cancellationToken);
                return;
            }

            var references = AbdmDiscoveryRules.SplitReferences(linkRequest.RequestedReferences);
            var careContexts = await _db.AbdmCareContexts
                .Where(c => c.HospitalId == hospitalId && c.PatientId == linkRequest.PatientId && references.Contains(c.ReferenceNumber))
                .ToListAsync(cancellationToken);

            foreach (var careContext in careContexts)
                await MarkLinkedAsync(careContext, linkRequest.AbhaAddress, now, cancellationToken);

            linkRequest.Status = AbdmLinkRequestStatus.Linked;
            linkRequest.OtpHash = null;
            linkRequest.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            var patientName = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.PatientId == linkRequest.PatientId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = "on-confirm",
                Path = OnConfirmPath,
                HospitalId = hospitalId,
                SubjectRef = linkRequest.AbhaAddress ?? linkRequest.PatientId,
                ExpectsCallback = false,
                Body = new
                {
                    patient = PatientEntries(linkRequest.PatientId, patientName, careContexts),
                    response = new { requestId = inbound.HeaderRequestId }
                }
            }, cancellationToken);
        }

        /// <summary>LinkStatus is a concurrency token: if the hospital's own link moved the row on
        /// in the meantime, reload and mark it once more.</summary>
        private async Task MarkLinkedAsync(AbdmCareContext careContext, string? abhaAddress, DateTime now, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                careContext.LinkStatus = AbdmLinkStatus.Linked;
                careContext.LinkedAt ??= now;
                careContext.AbhaAddress ??= abhaAddress;
                careContext.LastErrorCode = null;
                careContext.LastErrorMessage = null;
                careContext.UpdatedAt = now;
                careContext.UpdatedBy = "patient (ABHA app)";
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                        await entry.ReloadAsync(cancellationToken);
                }
            }
        }

        // ---- replies -------------------------------------------------------------------------

        /// <summary>One entry per HI type, each listing the care contexts that hold that type — the
        /// same shape HIP-initiated linking sends.</summary>
        private static object[] PatientEntries(string patientId, string? patientName, IReadOnlyCollection<AbdmCareContext> careContexts) =>
            careContexts
                .SelectMany(c => AbdmDiscoveryRules.SplitReferences(c.HiTypes).Select(hiType => new { hiType, careContext = c }))
                .GroupBy(x => x.hiType)
                .Select(group => (object)new
                {
                    referenceNumber = patientId,
                    display = string.IsNullOrWhiteSpace(patientName) ? patientId : patientName,
                    careContexts = group.Select(x => new { referenceNumber = x.careContext.ReferenceNumber, display = x.careContext.Display }).ToArray(),
                    hiType = group.Key,
                    count = group.Count()
                })
                .ToArray();

        private async Task ReplyErrorAsync(string kind, string path, AbdmInbound inbound, string? transactionId, string code, string message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ABDM {Kind} answered with {Code} for REQUEST-ID {RequestId}.", kind, code, inbound.HeaderRequestId ?? "none");
            await _hie.SendAsync(new AbdmHieCall
            {
                Kind = kind,
                Path = path,
                HospitalId = inbound.HospitalId!.Value,
                FacilityHeader = kind == "on-discover" ? AbdmFacilityHeader.HipAndHiu : AbdmFacilityHeader.Hip,
                SubjectRef = code,
                ExpectsCallback = false,
                Body = transactionId == null
                    ? (object)new { error = new { code, message }, response = new { requestId = inbound.HeaderRequestId } }
                    : new { transactionId, error = new { code, message }, response = new { requestId = inbound.HeaderRequestId } }
            }, cancellationToken);
        }
    }
}
