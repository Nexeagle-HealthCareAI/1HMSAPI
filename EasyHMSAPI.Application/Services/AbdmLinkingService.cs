using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>A refusal to start linking, decided here before anything is sent to ABDM.</summary>
    public sealed class AbdmLinkingRefusedException : InvalidOperationException
    {
        public AbdmLinkingRefusedException(string message) : base(message) { }
    }

    public interface IAbdmLinkingService
    {
        /// <summary>Starts linking an OPD visit or IPD admission and returns at once with the care
        /// context as it stands; the rest of the exchange happens on ABDM's callbacks.</summary>
        Task<AbdmCareContext> StartAsync(Guid hospitalId, string sourceType, Guid sourceId, string? userName, CancellationToken cancellationToken);
        Task OnLinkTokenAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken);
        Task OnLinkAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken);
    }

    /// <summary>
    /// HIP-initiated linking (abdm-m2 journeys "Link token" then "HIP initiated linking"):
    /// generate-token → on-generate-token → link/carecontext → on_carecontext. Each step is claimed
    /// on the care context row before it is sent, so the button and the callback can't both send it.
    /// </summary>
    public sealed class AbdmLinkingService : IAbdmLinkingService
    {
        private const string GenerateTokenPath = "/api/hiecm/v3/token/generate-token";
        private const string LinkCareContextPath = "/api/hiecm/hip/v3/link/carecontext";

        private readonly AppDbContext _db;
        private readonly IAbdmHieService _hie;
        private readonly ILogger<AbdmLinkingService> _logger;

        public AbdmLinkingService(AppDbContext db, IAbdmHieService hie, ILogger<AbdmLinkingService> logger)
        {
            _db = db;
            _hie = hie;
            _logger = logger;
        }

        public async Task<AbdmCareContext> StartAsync(Guid hospitalId, string sourceType, Guid sourceId, string? userName, CancellationToken cancellationToken)
        {
            var (patientId, whenUtc) = await LoadSourceAsync(hospitalId, sourceType, sourceId, cancellationToken);
            var patient = await _db.PatientRegistrations
                .Where(p => p.HospitalId == hospitalId && p.PatientId == patientId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new AbdmLinkingRefusedException("The patient for this visit was not found.");
            if (string.IsNullOrWhiteSpace(patient.AbhaId))
                throw new AbdmLinkingRefusedException("This patient has no ABHA. Link their ABHA first (ABHA / ABDM → Link Existing ABHA), then link this visit.");

            var abha = await FindAbhaAccountAsync(hospitalId, patient.AbhaId, cancellationToken);
            if (abha == null || string.IsNullOrWhiteSpace(abha.AbhaAddress))
                throw new AbdmLinkingRefusedException("We have this patient's ABHA number but not their ABHA address, which ABDM needs for linking. Verify their ABHA again to fetch it.");

            var hiTypes = await HiTypesAsync(hospitalId, sourceType, sourceId, cancellationToken);
            if (hiTypes.Count == 0)
                throw new AbdmLinkingRefusedException("Nothing to link yet: this visit has no prescription, vitals, lab report or discharge summary.");

            var careContext = await _db.AbdmCareContexts.FirstOrDefaultAsync(
                c => c.HospitalId == hospitalId && c.SourceType == sourceType && c.SourceId == sourceId, cancellationToken);
            var now = DateTime.UtcNow;
            if (careContext == null)
            {
                careContext = new AbdmCareContext
                {
                    CareContextId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    PatientId = patientId,
                    SourceType = sourceType,
                    SourceId = sourceId,
                    ReferenceNumber = AbdmCareContextRules.ReferenceNumber(sourceType, sourceId),
                    Display = AbdmCareContextRules.Display(sourceType, whenUtc),
                    CreatedAt = now
                };
                _db.AbdmCareContexts.Add(careContext);
            }
            else if (careContext.LinkStatus is AbdmLinkStatus.Linked or AbdmLinkStatus.TokenRequested or AbdmLinkStatus.LinkRequested)
            {
                // Already linked, or in flight: a second request would only come back as a duplicate.
                return careContext;
            }

            careContext.HiTypes = string.Join(',', hiTypes);
            careContext.AbhaNumber = abha.AbhaNumber;
            careContext.AbhaAddress = abha.AbhaAddress;
            careContext.LastErrorCode = null;
            careContext.LastErrorMessage = null;
            careContext.UpdatedAt = now;
            careContext.UpdatedBy = userName;

            var token = await FindUsableTokenAsync(hospitalId, abha.AbhaAddress!, cancellationToken);
            if (token != null)
            {
                careContext.LinkStatus = AbdmLinkStatus.TokenRequested;
                await _db.SaveChangesAsync(cancellationToken);
                await TrySendLinkAsync(careContext, token, cancellationToken);
                return careContext;
            }

            // Claim the token step first, then ask: a callback that beats the HTTP response finds
            // this row already waiting for it.
            careContext.LinkStatus = AbdmLinkStatus.TokenRequested;
            await _db.SaveChangesAsync(cancellationToken);
            try
            {
                var result = await _hie.SendAsync(new AbdmHieCall
                {
                    Kind = "link-token",
                    Path = GenerateTokenPath,
                    HospitalId = hospitalId,
                    SubjectRef = abha.AbhaAddress,
                    ExpectsCallback = true,
                    Body = new
                    {
                        abhaNumber = AbdmCareContextRules.AbhaNumberValue(abha.AbhaNumber),
                        abhaAddress = abha.AbhaAddress,
                        name = abha.FullName ?? patient.FullName,
                        gender = AbdmCareContextRules.GenderCode(abha.Gender) ?? AbdmCareContextRules.GenderCode(patient.Sex),
                        yearOfBirth = AbdmCareContextRules.YearOfBirth(abha.DateOfBirth, patient.DateOfBirth)
                    }
                }, cancellationToken);

                await _db.Entry(careContext).ReloadAsync(cancellationToken);
                careContext.TokenExchangeId ??= result.ExchangeId;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (AbdmException ex)
            {
                await FailAsync(careContext, ex.Code, ex.Message, cancellationToken);
                return careContext;
            }

            // The token may already have arrived while we waited for the 202.
            if (careContext.LinkStatus == AbdmLinkStatus.TokenRequested)
            {
                var arrived = await FindUsableTokenAsync(hospitalId, abha.AbhaAddress!, cancellationToken);
                if (arrived != null)
                    await TrySendLinkAsync(careContext, arrived, cancellationToken);
            }
            return careContext;
        }

        public async Task OnLinkTokenAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            var exchange = await _db.AbdmExchanges.FirstOrDefaultAsync(e => e.RequestId == inbound.AnswersRequestId, cancellationToken);
            if (exchange?.HospitalId == null)
                return;
            var hospitalId = exchange.HospitalId.Value;
            var abhaAddress = AbdmCareContextRules.FindString(inbound.RawBody, "abhaAddress") ?? exchange.SubjectRef ?? string.Empty;

            var waiting = await _db.AbdmCareContexts
                .Where(c => c.HospitalId == hospitalId && c.LinkStatus == AbdmLinkStatus.TokenRequested
                    && (c.TokenExchangeId == exchange.ExchangeId || c.AbhaAddress == abhaAddress))
                .ToListAsync(cancellationToken);

            if (exchange.ErrorCode != null)
            {
                foreach (var careContext in waiting)
                    await FailAsync(careContext, exchange.ErrorCode, exchange.ErrorMessage ?? "ABDM refused the link token request.", cancellationToken);
                return;
            }

            var linkToken = AbdmCareContextRules.FindString(inbound.RawBody, "linkToken");
            if (linkToken == null)
            {
                foreach (var careContext in waiting)
                    await FailAsync(careContext, "LOCAL-NO-LINK-TOKEN", "ABDM answered the link token request without a link token.", cancellationToken);
                return;
            }

            _db.AbdmLinkTokens.Add(new AbdmLinkToken
            {
                LinkTokenId = Guid.NewGuid(),
                HospitalId = hospitalId,
                HipId = exchange.FacilityId ?? string.Empty,
                AbhaAddress = abhaAddress,
                Token = linkToken,
                ExpiresAt = AbdmCareContextRules.JwtExpiry(linkToken),
                SourceExchangeId = exchange.ExchangeId,
                ReceivedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var careContext in waiting)
                await TrySendLinkAsync(careContext, linkToken, cancellationToken);
        }

        public async Task OnLinkAnsweredAsync(AbdmInbound inbound, CancellationToken cancellationToken)
        {
            var exchange = await _db.AbdmExchanges.FirstOrDefaultAsync(e => e.RequestId == inbound.AnswersRequestId, cancellationToken);
            if (exchange?.HospitalId == null)
                return;

            var careContext = await _db.AbdmCareContexts.FirstOrDefaultAsync(c =>
                c.HospitalId == exchange.HospitalId
                && (c.LinkExchangeId == exchange.ExchangeId || c.ReferenceNumber == exchange.SubjectRef), cancellationToken);
            if (careContext == null || careContext.LinkStatus != AbdmLinkStatus.LinkRequested)
                return;

            if (exchange.ErrorCode != null)
            {
                await FailAsync(careContext, exchange.ErrorCode, exchange.ErrorMessage ?? "ABDM refused the link.", cancellationToken);
                return;
            }

            careContext.LinkStatus = AbdmLinkStatus.Linked;
            careContext.LinkedAt = DateTime.UtcNow;
            careContext.LinkExchangeId ??= exchange.ExchangeId;
            careContext.UpdatedAt = DateTime.UtcNow;
            await SaveIfStillOursAsync(cancellationToken);
        }

        // ---- steps ---------------------------------------------------------------------------

        /// <summary>Claims TOKEN_REQUESTED → LINK_REQUESTED, then sends. Losing the claim means the
        /// other worker is already sending it.</summary>
        private async Task TrySendLinkAsync(AbdmCareContext careContext, string linkToken, CancellationToken cancellationToken)
        {
            careContext.LinkStatus = AbdmLinkStatus.LinkRequested;
            careContext.UpdatedAt = DateTime.UtcNow;
            if (!await SaveIfStillOursAsync(cancellationToken))
                return;

            var patientDisplay = await _db.PatientRegistrations
                .Where(p => p.HospitalId == careContext.HospitalId && p.PatientId == careContext.PatientId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            try
            {
                var result = await _hie.SendAsync(new AbdmHieCall
                {
                    Kind = "link-carecontext",
                    Path = LinkCareContextPath,
                    HospitalId = careContext.HospitalId,
                    SubjectRef = careContext.ReferenceNumber,
                    ExpectsCallback = true,
                    LinkToken = linkToken,
                    Body = new
                    {
                        abhaNumber = AbdmCareContextRules.AbhaNumberValue(careContext.AbhaNumber),
                        abhaAddress = careContext.AbhaAddress,
                        // One entry per HI type, each listing this care context.
                        patient = careContext.HiTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(hiType => new
                        {
                            referenceNumber = careContext.PatientId,
                            display = string.IsNullOrWhiteSpace(patientDisplay) ? careContext.PatientId : patientDisplay,
                            careContexts = new[] { new { referenceNumber = careContext.ReferenceNumber, display = careContext.Display } },
                            hiType,
                            count = 1
                        }).ToArray()
                    }
                }, cancellationToken);

                await _db.Entry(careContext).ReloadAsync(cancellationToken);
                careContext.LinkExchangeId ??= result.ExchangeId;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (AbdmException ex)
            {
                await FailAsync(careContext, ex.Code, ex.Message, cancellationToken);
            }
        }

        private async Task FailAsync(AbdmCareContext careContext, string? code, string message, CancellationToken cancellationToken)
        {
            await _db.Entry(careContext).ReloadAsync(cancellationToken);
            careContext.LinkStatus = AbdmLinkStatus.Failed;
            careContext.LastErrorCode = code;
            careContext.LastErrorMessage = message.Length > 500 ? message[..500] : message;
            careContext.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("ABDM linking failed for care context {Reference}: {Code}.", careContext.ReferenceNumber, code ?? "none");
        }

        private async Task<bool> SaveIfStillOursAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(cancellationToken);
                return false;
            }
        }

        // ---- lookups -------------------------------------------------------------------------

        private async Task<(string PatientId, DateTime WhenUtc)> LoadSourceAsync(Guid hospitalId, string sourceType, Guid sourceId, CancellationToken cancellationToken)
        {
            if (sourceType == AbdmCareContextSource.Opd)
            {
                var appointment = await _db.Appointments
                    .Where(a => a.ApptId == sourceId && a.HospitalId == hospitalId)
                    .Select(a => new { a.PatientId, a.ApptDate })
                    .FirstOrDefaultAsync(cancellationToken);
                if (appointment == null || string.IsNullOrWhiteSpace(appointment.PatientId))
                    throw new AbdmLinkingRefusedException("This OPD visit was not found in this hospital.");
                return (appointment.PatientId, appointment.ApptDate);
            }
            if (sourceType == AbdmCareContextSource.Ipd)
            {
                var admission = await _db.Admission
                    .Where(a => a.AdmissionId == sourceId && a.HospitalId == hospitalId)
                    .Select(a => new { a.PatientId, a.AdmittedAt })
                    .FirstOrDefaultAsync(cancellationToken);
                if (admission == null)
                    throw new AbdmLinkingRefusedException("This admission was not found in this hospital.");
                return (admission.PatientId, admission.AdmittedAt);
            }
            throw new AbdmLinkingRefusedException("Only OPD visits and IPD admissions can be linked.");
        }

        private async Task<AbhaAccount?> FindAbhaAccountAsync(Guid hospitalId, string abhaId, CancellationToken cancellationToken)
        {
            var accounts = await _db.AbhaAccount.Where(a => a.HospitalId == hospitalId).ToListAsync(cancellationToken);
            if (abhaId.Contains('@'))
                return accounts.FirstOrDefault(a => string.Equals(a.AbhaAddress, abhaId.Trim(), StringComparison.OrdinalIgnoreCase));
            var digits = AbdmCareContextRules.Digits(abhaId);
            return accounts
                .Where(a => AbdmCareContextRules.Digits(a.AbhaNumber) == digits)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
        }

        private async Task<IReadOnlyList<string>> HiTypesAsync(Guid hospitalId, string sourceType, Guid sourceId, CancellationToken cancellationToken)
        {
            if (sourceType == AbdmCareContextSource.Opd)
            {
                var prescription = await _db.Prescription.AnyAsync(p => p.ApptId == sourceId && p.HospitalId == hospitalId, cancellationToken);
                var vitals = await _db.AppointmentVitals.AnyAsync(v => v.ApptId == sourceId && v.HospitalId == hospitalId, cancellationToken);
                var encounterIds = await _db.Encounter
                    .Where(e => e.HospitalId == hospitalId && e.SourceId == sourceId)
                    .Select(e => (Guid?)e.EncounterId)
                    .ToListAsync(cancellationToken);
                var labReport = encounterIds.Count > 0 && await _db.PathologyReport.AnyAsync(r => r.HospitalId == hospitalId
                    && _db.PathologyOrder.Any(o => o.OrderId == r.OrderId && encounterIds.Contains(o.EncounterId)), cancellationToken);
                return AbdmCareContextRules.HiTypes(prescription, vitals, labReport, dischargeSummary: false);
            }

            var discharge = await _db.DischargeSummary.AnyAsync(d => d.AdmissionId == sourceId && d.HospitalId == hospitalId && d.SignedAt != null, cancellationToken);
            var ipdLab = await _db.PathologyReport.AnyAsync(r => r.HospitalId == hospitalId
                && _db.PathologyOrder.Any(o => o.OrderId == r.OrderId && o.AdmissionId == sourceId), cancellationToken);
            return AbdmCareContextRules.HiTypes(prescription: false, vitals: false, labReport: ipdLab, dischargeSummary: discharge);
        }

        private async Task<string?> FindUsableTokenAsync(Guid hospitalId, string abhaAddress, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var tokens = await _db.AbdmLinkTokens
                .Where(t => t.HospitalId == hospitalId && t.AbhaAddress == abhaAddress)
                .OrderByDescending(t => t.ReceivedAt)
                .Select(t => new { t.Token, t.ExpiresAt })
                .Take(3)
                .ToListAsync(cancellationToken);
            var stored = tokens.FirstOrDefault(t => AbdmCareContextRules.IsUsable(t.ExpiresAt, now))?.Token;
            if (stored != null)
                return stored;

            // A patient who scanned our counter QR handed us a link token with their profile (M1).
            var shared = await _db.AbdmProfileShares
                .Where(s => s.HospitalId == hospitalId && s.AbhaAddress == abhaAddress && s.LinkToken != null)
                .OrderByDescending(s => s.ReceivedAt)
                .Select(s => s.LinkToken)
                .FirstOrDefaultAsync(cancellationToken);
            return shared != null && AbdmCareContextRules.IsUsable(AbdmCareContextRules.JwtExpiry(shared), now) ? shared : null;
        }
    }
}
