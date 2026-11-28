using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>What the sweep does for one visit or admission that has a new record.</summary>
    public enum AbdmAutoLinkAction
    {
        Skip,
        /// <summary>The patient holds an ABHA: link the care context (HIP_INTI_LINK_504/505).</summary>
        Link,
        /// <summary>The patient gave a mobile and no ABHA: ABDM texts them a deep link (HIP_INIT_NOTIFY_HIECM).</summary>
        Sms
    }

    public static class AbdmAutoLinkRules
    {
        /// <param name="abhaId">The ABHA number or address on the patient's registration, if any.</param>
        /// <param name="linkStatus">The care context's link status, or null when there is no care context yet.</param>
        /// <param name="smsAlreadySent">Whether this visit's record SMS has already gone out.</param>
        public static AbdmAutoLinkAction Decide(string? abhaId, string? mobile, string? linkStatus, bool smsAlreadySent)
        {
            if (!string.IsNullOrWhiteSpace(abhaId))
            {
                // A failed link is left for staff to retry from the visit, so the sweep never repeats a refusal.
                return linkStatus == null || linkStatus == AbdmLinkStatus.NotLinked ? AbdmAutoLinkAction.Link : AbdmAutoLinkAction.Skip;
            }
            if (smsAlreadySent || AbdmCareContextRules.Mobile10(mobile) == null)
                return AbdmAutoLinkAction.Skip;
            return AbdmAutoLinkAction.Sms;
        }
    }

    public sealed record AbdmAutoLinkResult(int LinksStarted, int SmsSent);

    public interface IAbdmAutoLinkService
    {
        Task<AbdmAutoLinkResult> SweepAsync(DateTime nowUtc, CancellationToken cancellationToken);
    }

    /// <summary>
    /// ABDM M2: a record saved in 1HMS reaches ABDM with no click at the desk. Each sweep looks at visits
    /// and admissions whose records changed in the last <see cref="LookbackHoursKey"/> hours, in every
    /// hospital with an ABDM facility (HIP id). A patient with an ABHA gets the care context linked; a
    /// patient with a mobile and no ABHA gets ABDM's deep-link SMS once per visit or admission.
    /// Switches: Abdm:AutoLink and Abdm:RecordSms ("false" turns each off).
    /// </summary>
    public sealed class AbdmAutoLinkService : IAbdmAutoLinkService
    {
        public const string AutoLinkKey = "Abdm:AutoLink";
        public const string RecordSmsKey = "Abdm:RecordSms";
        public const string LookbackHoursKey = "Abdm:AutoLinkLookbackHours";
        public const string RecordSmsKind = "record-sms";
        private const int DefaultLookbackHours = 48;
        private const int MaxPerHospitalPerSweep = 50;

        private readonly AppDbContext _db;
        private readonly IAbdmLinkingService _linking;
        private readonly IAbdmHieService _hie;
        private readonly ILogger<AbdmAutoLinkService> _logger;
        private readonly bool _autoLink;
        private readonly bool _recordSms;
        private readonly TimeSpan _lookback;

        public AbdmAutoLinkService(AppDbContext db, IAbdmLinkingService linking, IAbdmHieService hie, ILogger<AbdmAutoLinkService> logger, IConfiguration? configuration = null)
        {
            _db = db;
            _linking = linking;
            _hie = hie;
            _logger = logger;
            _autoLink = !string.Equals(configuration?[AutoLinkKey], "false", StringComparison.OrdinalIgnoreCase);
            _recordSms = !string.Equals(configuration?[RecordSmsKey], "false", StringComparison.OrdinalIgnoreCase);
            _lookback = TimeSpan.FromHours(int.TryParse(configuration?[LookbackHoursKey], out var hours) && hours > 0 ? hours : DefaultLookbackHours);
        }

        public async Task<AbdmAutoLinkResult> SweepAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            if (!_autoLink && !_recordSms)
                return new AbdmAutoLinkResult(0, 0);

            var since = nowUtc - _lookback;
            var facilities = await _db.AbdmFacilities.AsNoTracking()
                .Where(f => f.HipId != null && f.HipId != "")
                .Select(f => new { f.HospitalId, f.HipId })
                .ToListAsync(cancellationToken);

            int links = 0, sms = 0;
            foreach (var facility in facilities)
            {
                var (l, s) = await SweepHospitalAsync(facility.HospitalId, facility.HipId, since, cancellationToken);
                links += l;
                sms += s;
            }
            return new AbdmAutoLinkResult(links, sms);
        }

        private async Task<(int Links, int Sms)> SweepHospitalAsync(Guid hospitalId, string hipId, DateTime since, CancellationToken cancellationToken)
        {
            var sources = await ChangedSourcesAsync(hospitalId, since, cancellationToken);
            if (sources.Count == 0)
                return (0, 0);

            var references = sources.Select(s => AbdmCareContextRules.ReferenceNumber(s.SourceType, s.SourceId)).ToList();
            var statuses = await _db.AbdmCareContexts.AsNoTracking()
                .Where(c => c.HospitalId == hospitalId && references.Contains(c.ReferenceNumber))
                .Select(c => new { c.ReferenceNumber, c.LinkStatus })
                .ToDictionaryAsync(c => c.ReferenceNumber, c => (string?)c.LinkStatus, cancellationToken);
            var smsSent = (await _db.AbdmExchanges.AsNoTracking()
                .Where(e => e.HospitalId == hospitalId && e.Kind == RecordSmsKind && e.SubjectRef != null && references.Contains(e.SubjectRef))
                .Select(e => e.SubjectRef!)
                .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

            var patientIds = sources.Select(s => s.PatientId).Distinct().ToList();
            var patients = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && patientIds.Contains(p.PatientId))
                .Select(p => new { p.PatientId, p.AbhaId, p.Mobile })
                .ToListAsync(cancellationToken);
            var byPatient = patients.GroupBy(p => p.PatientId).ToDictionary(g => g.Key, g => g.First());

            string? hospitalName = null;
            int links = 0, sms = 0, done = 0;
            foreach (var source in sources)
            {
                if (done >= MaxPerHospitalPerSweep)
                    break;
                if (!byPatient.TryGetValue(source.PatientId, out var patient))
                    continue;
                var reference = AbdmCareContextRules.ReferenceNumber(source.SourceType, source.SourceId);
                statuses.TryGetValue(reference, out var status);
                var action = AbdmAutoLinkRules.Decide(patient.AbhaId, patient.Mobile, status, smsSent.Contains(reference));

                if (action == AbdmAutoLinkAction.Link && _autoLink)
                {
                    done++;
                    try
                    {
                        var careContext = await _linking.StartAsync(hospitalId, source.SourceType, source.SourceId, "abdm-auto-link", cancellationToken);
                        if (careContext.LinkStatus != AbdmLinkStatus.Failed)
                            links++;
                    }
                    catch (AbdmLinkingRefusedException ex)
                    {
                        // Nothing to link yet, or the ABHA address is missing: shown on the visit, not retried here.
                        _logger.LogInformation("ABDM auto-link skipped {Reference}: {Reason}", reference, ex.Message);
                    }
                }
                else if (action == AbdmAutoLinkAction.Sms && _recordSms)
                {
                    done++;
                    hospitalName ??= await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == hospitalId).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
                    try
                    {
                        await _hie.SendAsync(new AbdmHieCall
                        {
                            Kind = RecordSmsKind,
                            Path = AbdmLinkingService.SmsNotifyPath,
                            HospitalId = hospitalId,
                            SubjectRef = reference,
                            ExpectsCallback = true,
                            FacilityHeader = AbdmFacilityHeader.None,
                            Body = new
                            {
                                notification = new
                                {
                                    phoneNo = AbdmCareContextRules.Mobile10(patient.Mobile),
                                    hip = new { id = hipId, name = hospitalName }
                                }
                            }
                        }, cancellationToken);
                        sms++;
                    }
                    catch (AbdmException ex)
                    {
                        _logger.LogWarning("ABDM record SMS for {Reference} was refused: {Code}.", reference, ex.Code ?? "none");
                    }
                }
            }
            return (links, sms);
        }

        private sealed record Source(string SourceType, Guid SourceId, string PatientId);

        /// <summary>OPD visits and IPD admissions with a record saved since <paramref name="since"/>, newest first.</summary>
        private async Task<List<Source>> ChangedSourcesAsync(Guid hospitalId, DateTime since, CancellationToken cancellationToken)
        {
            var opdIds = await _db.Prescription.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.UpdatedAt >= since)
                .Select(p => p.ApptId)
                .ToListAsync(cancellationToken);

            var labOrders = await _db.PathologyReport.AsNoTracking()
                .Where(r => r.HospitalId == hospitalId && r.ApprovedAt != null && r.ApprovedAt >= since)
                .Join(_db.PathologyOrder.AsNoTracking(), r => r.OrderId, o => o.OrderId, (r, o) => new { o.AdmissionId, o.EncounterId })
                .ToListAsync(cancellationToken);
            var labEncounterIds = labOrders.Where(o => o.AdmissionId == null && o.EncounterId != null).Select(o => o.EncounterId!.Value).Distinct().ToList();
            if (labEncounterIds.Count > 0)
            {
                opdIds.AddRange(await _db.Encounter.AsNoTracking()
                    .Where(e => e.HospitalId == hospitalId && labEncounterIds.Contains(e.EncounterId) && e.SourceId != null)
                    .Select(e => e.SourceId!.Value)
                    .ToListAsync(cancellationToken));
            }

            var ipdIds = await _db.DischargeSummary.AsNoTracking()
                .Where(d => d.HospitalId == hospitalId && d.SignedAt != null && d.SignedAt >= since)
                .Select(d => d.AdmissionId)
                .ToListAsync(cancellationToken);
            ipdIds.AddRange(labOrders.Where(o => o.AdmissionId != null).Select(o => o.AdmissionId!.Value));

            // Uploaded documents (Health Document record).
            opdIds.AddRange(await _db.PrescriptionAttachments.AsNoTracking()
                .Where(a => a.HospitalId == hospitalId && a.UploadedAt != null && a.UploadedAt >= since)
                .Select(a => a.ApptId)
                .ToListAsync(cancellationToken));
            ipdIds.AddRange(await _db.AdmissionDocument.AsNoTracking()
                .Where(d => d.HospitalId == hospitalId && d.UploadedAt >= since)
                .Select(d => d.AdmissionId)
                .ToListAsync(cancellationToken));

            // Bills finalised (Invoice record): the encounter names the visit or the admission.
            var billedEncounterIds = await _db.BillingInvoice.AsNoTracking()
                .Where(i => i.HospitalId == hospitalId && i.FinalizedAt != null && i.FinalizedAt >= since && i.CancelledAt == null)
                .Select(i => i.EncounterId)
                .ToListAsync(cancellationToken);
            if (billedEncounterIds.Count > 0)
            {
                var billed = await _db.Encounter.AsNoTracking()
                    .Where(e => e.HospitalId == hospitalId && billedEncounterIds.Contains(e.EncounterId) && e.SourceId != null)
                    .Select(e => e.SourceId!.Value)
                    .ToListAsync(cancellationToken);
                // Which kind it is is settled below: only ids that are an appointment or an admission survive.
                opdIds.AddRange(billed);
                ipdIds.AddRange(billed);
            }

            opdIds = opdIds.Distinct().ToList();
            ipdIds = ipdIds.Distinct().ToList();

            var sources = new List<(Source Source, DateTime When)>();
            if (opdIds.Count > 0)
            {
                var visits = await _db.Appointments.AsNoTracking()
                    .Where(a => a.HospitalId == hospitalId && opdIds.Contains(a.ApptId) && a.PatientId != null && a.PatientId != "")
                    .Select(a => new { a.ApptId, a.PatientId, a.ApptDate })
                    .ToListAsync(cancellationToken);
                sources.AddRange(visits.Select(v => (new Source(AbdmCareContextSource.Opd, v.ApptId, v.PatientId!), v.ApptDate)));
            }
            if (ipdIds.Count > 0)
            {
                var admissions = await _db.Admission.AsNoTracking()
                    .Where(a => a.HospitalId == hospitalId && ipdIds.Contains(a.AdmissionId))
                    .Select(a => new { a.AdmissionId, a.PatientId, a.AdmittedAt })
                    .ToListAsync(cancellationToken);
                sources.AddRange(admissions.Select(a => (new Source(AbdmCareContextSource.Ipd, a.AdmissionId, a.PatientId), a.AdmittedAt)));
            }
            return sources.OrderByDescending(s => s.When).Select(s => s.Source).ToList();
        }
    }
}
