using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Scans open IPD admissions and raises operational alerts for three rules:
    //   EDD_BREACH      - expected discharge date has passed
    //   DEPOSIT_LOW     - advance deposits below the supplied threshold
    //   CONSENT_PENDING - no general-admission consent recorded after a grace period
    // Idempotent per (admission, code): an existing ACTIVE alert is treated as a duplicate.
    public class EvaluateAlertsHandler : IRequestHandler<EvaluateAlertsRequestModel, EvaluateAlertsResponseModel>
    {
        private readonly AppDbContext _context;

        private const string EddBreachCode = "EDD_BREACH";
        private const string DepositLowCode = "DEPOSIT_LOW";
        private const string ConsentPendingCode = "CONSENT_PENDING";
        private const string GeneralAdmissionConsent = "GENERAL_ADMISSION";

        public EvaluateAlertsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<EvaluateAlertsResponseModel> Handle(EvaluateAlertsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var now = DateTime.UtcNow;
                var graceHours = request.ConsentPendingGraceHours is > 0 ? request.ConsentPendingGraceHours!.Value : 24;
                var consentCutoff = now.AddHours(-graceHours);

                var admissions = await _context.Admission.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId && a.StatusCode == "ADMITTED")
                    .ToListAsync(cancellationToken);

                var response = new EvaluateAlertsResponseModel { Success = true, AdmissionsScanned = admissions.Count };
                if (admissions.Count == 0)
                {
                    response.Message = "No open admissions to evaluate.";
                    return response;
                }

                var admissionIds = admissions.Select(a => a.AdmissionId).ToList();
                var encounterIds = admissions.Select(a => a.EncounterId).Distinct().ToList();

                // Existing ACTIVE alerts for these admissions → dedup keys "{admissionId}|{code}".
                var existingKeys = (await _context.Alert.AsNoTracking()
                        .Where(a => a.HospitalId == request.HospitalId
                                    && a.Status == "ACTIVE"
                                    && a.AdmissionId != null
                                    && admissionIds.Contains(a.AdmissionId.Value))
                        .Select(a => new { a.AdmissionId, a.AlertCode })
                        .ToListAsync(cancellationToken))
                    .Select(x => $"{x.AdmissionId}|{x.AlertCode}")
                    .ToHashSet();

                // Advance deposits per encounter.
                var advanceByEncounter = (await _context.BillingPayment.AsNoTracking()
                        .Where(p => p.HospitalId == request.HospitalId
                                    && p.PaymentType == "ADVANCE"
                                    && p.EncounterId != Guid.Empty
                                    && encounterIds.Contains(p.EncounterId))
                        .GroupBy(p => p.EncounterId)
                        .Select(g => new { EncounterId = g.Key, Total = g.Sum(x => x.Amount) })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.EncounterId, x => x.Total);

                // Admissions that already have a general-admission consent on file.
                var admissionsWithConsent = (await _context.ConsentRecord.AsNoTracking()
                        .Where(c => c.HospitalId == request.HospitalId
                                    && c.TemplateTypeCode == GeneralAdmissionConsent
                                    && admissionIds.Contains(c.AdmissionId))
                        .Select(c => c.AdmissionId)
                        .Distinct()
                        .ToListAsync(cancellationToken))
                    .ToHashSet();

                var threshold = request.DepositLowThresholdAmount;

                foreach (var adm in admissions)
                {
                    // EDD breach
                    if (adm.ExpectedDischargeAt.HasValue && adm.ExpectedDischargeAt.Value < now)
                    {
                        if (TryRaise(adm, EddBreachCode, "Expected discharge overdue",
                                $"Expected discharge was {adm.ExpectedDischargeAt:dd MMM yyyy HH:mm} and the patient is still admitted.",
                                now, request, existingKeys))
                            response.EddBreachRaised++;
                        else
                            response.AlertsSkippedDuplicate++;
                    }

                    // Deposit low (only when a threshold is provided)
                    if (threshold.HasValue && threshold.Value > 0)
                    {
                        var deposit = adm.EncounterId.HasValue && advanceByEncounter.TryGetValue(adm.EncounterId.Value, out var d) ? d : 0m;
                        if (deposit < threshold.Value)
                        {
                            if (TryRaise(adm, DepositLowCode, "Advance deposit low",
                                    $"Advance deposit ₹{deposit:0.00} is below the ₹{threshold.Value:0.00} threshold.",
                                    now, request, existingKeys))
                                response.DepositLowRaised++;
                            else
                                response.AlertsSkippedDuplicate++;
                        }
                    }

                    // Consent pending
                    if (!admissionsWithConsent.Contains(adm.AdmissionId) && adm.AdmittedAt < consentCutoff)
                    {
                        if (TryRaise(adm, ConsentPendingCode, "Admission consent pending",
                                $"No general-admission consent recorded since admission on {adm.AdmittedAt:dd MMM yyyy HH:mm}.",
                                now, request, existingKeys))
                            response.ConsentPendingRaised++;
                        else
                            response.AlertsSkippedDuplicate++;
                    }
                }

                response.AlertsRaised = response.EddBreachRaised + response.DepositLowRaised + response.ConsentPendingRaised;

                if (response.AlertsRaised > 0)
                    await _context.SaveChangesAsync(cancellationToken);

                response.Message = $"Scanned {response.AdmissionsScanned} admissions; raised {response.AlertsRaised} alerts.";
                return response;
            }
            catch (Exception ex)
            {
                return new EvaluateAlertsResponseModel { Success = false, Message = ex.Message };
            }
        }

        // Adds an in-app WARNING alert unless one is already active for this (admission, code).
        // Returns true when a new alert was added, false when skipped as a duplicate.
        private bool TryRaise(Admission adm, string code, string title, string body, DateTime now,
            EvaluateAlertsRequestModel request, HashSet<string> existingKeys)
        {
            var key = $"{adm.AdmissionId}|{code}";
            if (!existingKeys.Add(key))
                return false;

            _context.Alert.Add(new Alert
            {
                AlertId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                AlertCode = code,
                Severity = "WARNING",
                Title = title,
                Body = body,
                PatientId = adm.PatientId,
                AdmissionId = adm.AdmissionId,
                EncounterId = adm.EncounterId,
                Status = "ACTIVE",
                RaisedAt = now,
                RaisedBy = request.LoggedInUserName,
                RaisedByUserId = request.LoggedInUserId,
                SourceModule = "ALERT_EVALUATOR",
                SourceRefId = adm.AdmissionId.ToString(),
                DispatchSms = false,
                DispatchWhatsApp = false,
                DispatchInApp = true,
                CreatedAt = now,
            });
            return true;
        }
    }
}
