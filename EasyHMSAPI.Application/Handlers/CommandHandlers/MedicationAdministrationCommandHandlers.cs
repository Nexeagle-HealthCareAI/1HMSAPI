using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// MAR — records one nurse action against a computed dose slot of a MEDICATION
    /// ClinicalOrderLine. Unlike CPOE's PlaceClinicalOrderRequestModel, this never re-bills
    /// (billing happened at order time — see ClinicalOrderCommandHandlers) so it's a plain
    /// single-entity insert, no explicit transaction: there's no cross-entity invariant to
    /// protect (two administration rows landing for the same slot is resolved by the read-side
    /// "closest/most-recent wins" matching rule in GetMarGridHandler, not prevented at write time).
    /// </summary>
    public class MedicationAdministrationCommandHandlers :
        IRequestHandler<RecordMedicationAdministrationRequestModel, RecordMedicationAdministrationResponseModel>
    {
        private readonly AppDbContext _context;

        public MedicationAdministrationCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordMedicationAdministrationResponseModel> Handle(RecordMedicationAdministrationRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.OrderLineId == Guid.Empty)
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "HospitalId and OrderLineId are required." };

                var actionStatus = request.ActionStatus?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(actionStatus) || !IpdConstants.MedicationActionStatus.All.Contains(actionStatus))
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "Invalid action status." };

                if (!request.FiveRightsConfirmed)
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "5-Rights confirmation is required before recording an administration." };

                if (actionStatus != IpdConstants.MedicationActionStatus.Administered && string.IsNullOrWhiteSpace(request.Reason))
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "A reason is required when a dose is not administered." };

                var line = await _context.ClinicalOrderLine
                    .FirstOrDefaultAsync(l => l.OrderLineId == request.OrderLineId && l.HospitalId == request.HospitalId, cancellationToken);
                if (line == null)
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "Order line not found." };

                var order = await _context.ClinicalOrder
                    .FirstOrDefaultAsync(o => o.OrderId == line.OrderId, cancellationToken);
                if (order == null || order.OrderType != IpdConstants.ClinicalOrderType.Medication)
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "This is not a medication order line." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == order.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "Admission is not active." };

                // Re-validate the slot is plausible against a freshly computed schedule — guards
                // against a stale/tampered client posting an arbitrary ScheduledFor. STAT/SOS and
                // legacy free-text-Frequency lines have no computed schedule, so any ScheduledFor
                // is accepted for them as an ad-hoc entry.
                var freq = line.Frequency?.Trim().ToUpperInvariant();
                var isComputed = freq != null && (
                    IpdConstants.MedicationFrequency.ClockTimes.ContainsKey(freq)
                    || IpdConstants.MedicationFrequency.IntervalHours.ContainsKey(freq)
                    || freq == IpdConstants.MedicationFrequency.Stat);
                if (isComputed)
                {
                    var windowStart = request.ScheduledFor.AddDays(-2);
                    var windowEnd = request.ScheduledFor.AddDays(2);
                    var slots = MarScheduleCalculator.ComputeSlots(line.Frequency, order.OrderedAt, line.DurationDays, windowStart, windowEnd);
                    var matches = slots.Any(s => Math.Abs((s - request.ScheduledFor).TotalMinutes) <= MarScheduleCalculator.MatchTolerance.TotalMinutes);
                    if (!matches)
                        return new RecordMedicationAdministrationResponseModel { Success = false, Message = "This is not a valid scheduled dose time for this order line." };
                }

                if (line.IsHighAlert && string.IsNullOrWhiteSpace(request.WitnessName))
                    return new RecordMedicationAdministrationResponseModel { Success = false, Message = "This is a high-alert medication — a witness name is required." };

                var now = DateTime.UtcNow;
                var record = new MedicationAdministration
                {
                    MedicationAdministrationId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    MedicationOrderId = null,
                    OrderLineId = line.OrderLineId,
                    ScheduledFor = request.ScheduledFor,
                    ActionStatus = actionStatus,
                    AdministeredDose = actionStatus == IpdConstants.MedicationActionStatus.Administered
                        ? (string.IsNullOrWhiteSpace(request.AdministeredDose) ? line.Dose : request.AdministeredDose.Trim())
                        : null,
                    AdministeredRoute = actionStatus == IpdConstants.MedicationActionStatus.Administered
                        ? (string.IsNullOrWhiteSpace(request.AdministeredRoute) ? line.Route : request.AdministeredRoute.Trim())
                        : null,
                    AdministrationSite = string.IsNullOrWhiteSpace(request.AdministrationSite) ? null : request.AdministrationSite.Trim(),
                    ActedAt = now,
                    ActedBy = request.LoggedInUserName,
                    ActedByUserId = request.LoggedInUserId,
                    WitnessRequired = line.IsHighAlert,
                    WitnessName = line.IsHighAlert ? request.WitnessName!.Trim() : null,
                    WitnessUserId = line.IsHighAlert ? request.WitnessUserId : null,
                    WitnessConfirmedAt = line.IsHighAlert ? now : null,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.MedicationAdministration.Add(record);

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordMedicationAdministrationResponseModel
                {
                    Success = true,
                    Message = "Administration recorded.",
                    MedicationAdministrationId = record.MedicationAdministrationId,
                    ActionStatus = record.ActionStatus,
                };
            }
            catch (Exception)
            {
                return new RecordMedicationAdministrationResponseModel { Success = false, Message = "Error recording administration." };
            }
        }
    }
}
