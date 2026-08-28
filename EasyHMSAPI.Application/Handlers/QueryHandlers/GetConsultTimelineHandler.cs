using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsultTimelineHandler : IRequestHandler<GetConsultTimelineRequestModel, GetConsultTimelineResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsultTimelineHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsultTimelineResponseModel> Handle(GetConsultTimelineRequestModel request, CancellationToken cancellationToken)
        {
            var targetDate = request.TargetDate ?? DateTime.UtcNow.Date;

            // Doctor's prescription validity window (days). 0 = never expires.
            var validDuration = await _context.PrescriptionSettings
                .Where(ps => ps.DoctorId == request.DoctorId)
                .Select(ps => (int?)ps.ValidDuration)
                .FirstOrDefaultAsync(cancellationToken);

            // Doctor's active OPD consult fee and free-follow-up window (if any).
            var doctorFee = await _context.DoctorFees
                .Where(f => f.HospitalId == request.HospitalId
                         && f.DoctorId == request.DoctorId
                         && f.FeeType == "OPD_CONSULT"
                         && f.IsActive)
                .Select(f => new { Amount = (decimal?)f.Amount, f.FreeFollowUpDays })
                .FirstOrDefaultAsync(cancellationToken);
            var freeFollowUpDays = doctorFee?.FreeFollowUpDays ?? 0;

            // Preview the next visit using the SAME rule the booking uses.
            var preview = await AppointmentTypeResolver.ResolveAsync(
                _context, request.HospitalId, request.PatientId, request.PatientId, null,
                request.DoctorId, targetDate, null, cancellationToken);

            var response = new GetConsultTimelineResponseModel
            {
                PrescriptionValidDays = validDuration ?? 0,
                // The free-follow-up window is always a finite number of days (or none) under the
                // DoctorFee.FreeFollowUpDays model - there is no "infinite" option to represent here.
                NeverExpires = false,
                NextVisit = new ConsultNextVisit
                {
                    AppointmentType = preview.AppointmentType,
                    FeeApplies = preview.FeeApplies,
                    Fee = preview.FeeApplies ? (doctorFee?.Amount ?? 0m) : 0m,
                }
            };

            // Appointment history for this patient + doctor (exclude cancelled), most recent first.
            var appts = await _context.Appointments
                .Where(a => a.HospitalId == request.HospitalId
                         && a.PatientId == request.PatientId
                         && a.DoctorId == request.DoctorId
                         && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled)
                .OrderByDescending(a => a.ApptDate)
                .Select(a => new { a.ApptId, a.ApptDate, a.AppointmentType, a.CurrentStatusCode })
                .Take(50)
                .ToListAsync(cancellationToken);

            if (appts.Count == 0)
                return response;

            var apptIds = appts.Select(a => a.ApptId).ToList();

            // appointment -> OPD encounter
            var encounters = await _context.Encounter
                .Where(e => e.HospitalId == request.HospitalId
                         && e.SourceType == "Appointments"
                         && e.SourceId != null
                         && apptIds.Contains(e.SourceId!.Value)
                         && e.EncounterTypeCode == BillingConstants.EncounterType.Opd)
                .Select(e => new { ApptId = e.SourceId!.Value, e.EncounterId })
                .ToListAsync(cancellationToken);

            var apptToEncounter = encounters
                .GroupBy(e => e.ApptId)
                .ToDictionary(g => g.Key, g => g.First().EncounterId);
            var encounterIds = encounters.Select(e => e.EncounterId).Distinct().ToList();

            // CONSULT charge per encounter (amount).
            var consultByEncounter = encounterIds.Count == 0
                ? new Dictionary<Guid, decimal>()
                : await _context.BillingChargeEvent
                    .Where(c => encounterIds.Contains(c.EncounterId) && c.CategoryCode == "CONSULT")
                    .GroupBy(c => c.EncounterId)
                    .Select(g => new { EncounterId = g.Key, Amount = g.Sum(x => x.NetAmount) })
                    .ToDictionaryAsync(x => x.EncounterId, x => x.Amount, cancellationToken);

            // Payments per encounter (sum + latest receipt no). Contains() on an empty list is a
            // false predicate, so no explicit empty-list guard is needed.
            var payments = await _context.BillingPayment
                .Where(p => encounterIds.Contains(p.EncounterId) && p.PaymentType == "PAYMENT")
                .Select(p => new { p.EncounterId, p.Amount, p.ReceiptNo, p.PaidAt })
                .ToListAsync(cancellationToken);

            var paidByEncounter = payments
                .GroupBy(p => p.EncounterId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var receiptByEncounter = payments
                .GroupBy(p => p.EncounterId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PaidAt).Select(x => x.ReceiptNo).FirstOrDefault());

            foreach (var a in appts)
            {
                var visit = new ConsultTimelineVisit
                {
                    AppointmentId = a.ApptId,
                    ApptDate = a.ApptDate,
                    AppointmentType = a.AppointmentType,
                    StatusCode = a.CurrentStatusCode,
                };

                if (apptToEncounter.TryGetValue(a.ApptId, out var encId))
                {
                    visit.EncounterId = encId;
                    if (consultByEncounter.TryGetValue(encId, out var amount))
                    {
                        visit.ConsultCharged = true;
                        visit.Amount = amount;
                        var paid = paidByEncounter.TryGetValue(encId, out var p) ? p : 0m;
                        visit.ConsultPaid = amount > 0 && paid >= amount;
                        if (visit.ConsultPaid && receiptByEncounter.TryGetValue(encId, out var rec))
                            visit.ReceiptNo = rec;
                    }
                }

                response.History.Add(visit);
            }

            // Anchor = most recent fee visit (New / Old-Fee), not vitals-only.
            var lastFee = response.History
                .Where(v => (string.Equals(v.AppointmentType, AppConstants.AppointmentType_New, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(v.AppointmentType, AppConstants.AppointmentType_OldFee, StringComparison.OrdinalIgnoreCase))
                          && v.StatusCode != AppConstants.AppointmentStatus_VitalsRequired)
                .OrderByDescending(v => v.ApptDate)
                .FirstOrDefault();

            response.LastFeeVisit = lastFee;
            response.LastPaidDate = response.History
                .Where(v => v.ConsultPaid)
                .OrderByDescending(v => v.ApptDate)
                .Select(v => (DateTime?)v.ApptDate)
                .FirstOrDefault();

            if (lastFee != null)
            {
                response.ValidUptoDate = AppointmentTypeResolver.CalculateFreeFollowUpUpto(lastFee.ApptDate, freeFollowUpDays);
                response.FreeFollowUpCount = response.History.Count(v =>
                    string.Equals(v.AppointmentType, AppConstants.AppointmentType_OldNoFee, StringComparison.OrdinalIgnoreCase)
                    && v.ApptDate >= lastFee.ApptDate);
            }

            return response;
        }
    }
}
