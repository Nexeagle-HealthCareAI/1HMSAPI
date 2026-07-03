using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// MAR grid for one admission, one IST calendar day. Follows the same "fetch raw rows, fetch
    /// related rows, fold together in-memory" style as GetClinicalOrdersHandler/GetBedBoardHandler
    /// — the per-slot status here is genuinely computed (MarScheduleCalculator), not just folded,
    /// but the overall shape (bulk-load, dictionary/group-by, project) matches house convention.
    /// </summary>
    public class GetMarGridHandler : IRequestHandler<GetMarGridRequestModel, GetMarGridResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMarGridHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMarGridResponseModel> Handle(GetMarGridRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetMarGridResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var dayStart = request.DayStartUtc;
                var dayEnd = dayStart.AddDays(1);

                var orders = await _context.ClinicalOrder
                    .Where(o => o.HospitalId == request.HospitalId
                        && o.AdmissionId == request.AdmissionId
                        && o.OrderType == IpdConstants.ClinicalOrderType.Medication)
                    .ToListAsync(cancellationToken);
                var orderIds = orders.Select(o => o.OrderId).ToList();
                var ordersById = orders.ToDictionary(o => o.OrderId);

                // Only lines whose own order was placed on/before the window end, and which were
                // either still ACTIVE at day end or discontinued after the window's start (so a
                // line stopped mid-day still shows its already-past slots for that day).
                var lines = await _context.ClinicalOrderLine
                    .Where(l => orderIds.Contains(l.OrderId)
                        && (l.StatusCode == IpdConstants.ClinicalOrderLineStatus.Active || l.UpdatedAt >= dayStart))
                    .OrderBy(l => l.DisplayOrder)
                    .ToListAsync(cancellationToken);

                var lineIds = lines.Select(l => l.OrderLineId).ToList();
                // Widen the administration fetch window beyond [dayStart,dayEnd) by the match
                // tolerance so a dose acted a little before midnight / after midnight still
                // matches the slot it belongs to.
                var fetchStart = dayStart - MarScheduleCalculator.MatchTolerance;
                var fetchEnd = dayEnd + MarScheduleCalculator.MatchTolerance;
                var administrations = await _context.MedicationAdministration
                    .Where(m => m.HospitalId == request.HospitalId
                        && m.OrderLineId != null && lineIds.Contains(m.OrderLineId.Value)
                        && m.ScheduledFor >= fetchStart && m.ScheduledFor <= fetchEnd)
                    .OrderByDescending(m => m.ActedAt)
                    .ToListAsync(cancellationToken);
                var adminsByLine = administrations.GroupBy(a => a.OrderLineId!.Value).ToDictionary(g => g.Key, g => g.ToList());

                var now = DateTime.UtcNow;
                var result = new List<MarLineItem>();

                foreach (var line in lines)
                {
                    var order = ordersById[line.OrderId];
                    var freq = line.Frequency?.Trim().ToUpperInvariant();
                    var isRecognized = freq != null && (
                        IpdConstants.MedicationFrequency.ClockTimes.ContainsKey(freq)
                        || IpdConstants.MedicationFrequency.IntervalHours.ContainsKey(freq)
                        || freq == IpdConstants.MedicationFrequency.Stat
                        || freq == IpdConstants.MedicationFrequency.Sos);

                    var computedSlots = MarScheduleCalculator.ComputeSlots(line.Frequency, order.OrderedAt, line.DurationDays, dayStart, dayEnd.AddTicks(-1));
                    var candidateAdmins = adminsByLine.TryGetValue(line.OrderLineId, out var a) ? a : new List<MedicationAdministration>();
                    var claimed = new HashSet<Guid>();

                    var slotItems = new List<MarSlotItem>();
                    foreach (var slot in computedSlots)
                    {
                        var match = candidateAdmins
                            .Where(m => !claimed.Contains(m.MedicationAdministrationId)
                                && Math.Abs((m.ScheduledFor - slot).TotalMinutes) <= MarScheduleCalculator.MatchTolerance.TotalMinutes)
                            .OrderBy(m => Math.Abs((m.ScheduledFor - slot).TotalMinutes))
                            .ThenByDescending(m => m.ActedAt)
                            .FirstOrDefault();

                        if (match != null)
                        {
                            claimed.Add(match.MedicationAdministrationId);
                            slotItems.Add(new MarSlotItem
                            {
                                ScheduledForUtc = slot,
                                Status = match.ActionStatus,
                                MedicationAdministrationId = match.MedicationAdministrationId,
                                ActedAt = match.ActedAt,
                                ActedBy = match.ActedBy,
                                AdministeredDose = match.AdministeredDose,
                                AdministeredRoute = match.AdministeredRoute,
                                Reason = match.Reason,
                                Notes = match.Notes,
                                WitnessRequired = match.WitnessRequired,
                                WitnessName = match.WitnessName,
                                WitnessConfirmedAt = match.WitnessConfirmedAt,
                            });
                        }
                        else
                        {
                            slotItems.Add(new MarSlotItem
                            {
                                ScheduledForUtc = slot,
                                Status = MarScheduleCalculator.DeriveSlotStatus(slot, now),
                                WitnessRequired = line.IsHighAlert,
                            });
                        }
                    }

                    // Ad-hoc administrations for this line that don't correspond to any computed
                    // slot (SOS/PRN doses, or extra doses on a legacy free-text-Frequency line) —
                    // still shown on the grid as their own unscheduled entries, timestamped by
                    // ActedAt, so nurses see everything given today even for PRN drugs.
                    var adHocAdmins = candidateAdmins.Where(m => !claimed.Contains(m.MedicationAdministrationId) && m.ActedAt >= dayStart && m.ActedAt < dayEnd);
                    foreach (var m in adHocAdmins)
                    {
                        slotItems.Add(new MarSlotItem
                        {
                            ScheduledForUtc = m.ScheduledFor,
                            Status = m.ActionStatus,
                            MedicationAdministrationId = m.MedicationAdministrationId,
                            ActedAt = m.ActedAt,
                            ActedBy = m.ActedBy,
                            AdministeredDose = m.AdministeredDose,
                            AdministeredRoute = m.AdministeredRoute,
                            Reason = m.Reason,
                            Notes = m.Notes,
                            WitnessRequired = m.WitnessRequired,
                            WitnessName = m.WitnessName,
                            WitnessConfirmedAt = m.WitnessConfirmedAt,
                        });
                    }

                    result.Add(new MarLineItem
                    {
                        OrderLineId = line.OrderLineId,
                        OrderId = line.OrderId,
                        ItemName = line.ItemName,
                        SaltName = line.SaltName,
                        Dose = line.Dose,
                        Route = line.Route,
                        Frequency = line.Frequency,
                        Instructions = line.Instructions,
                        IsHighAlert = line.IsHighAlert,
                        OrderLineStatusCode = line.StatusCode,
                        IsAdHocOnly = !isRecognized || freq == IpdConstants.MedicationFrequency.Sos,
                        Slots = slotItems.OrderBy(s => s.ScheduledForUtc).ToList(),
                    });
                }

                return new GetMarGridResponseModel { Success = true, DayStartUtc = dayStart, DayEndUtc = dayEnd, Lines = result };
            }
            catch (Exception)
            {
                return new GetMarGridResponseModel { Success = false, Message = "Error loading MAR grid." };
            }
        }
    }
}
