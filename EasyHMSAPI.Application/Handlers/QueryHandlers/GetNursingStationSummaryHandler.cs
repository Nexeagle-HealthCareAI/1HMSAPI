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
    /// The Nursing Station's "my patients" list: every active admission in a bed on a ward this
    /// nurse is currently rostered to, with last vitals and MAR due/overdue folded in. Bulk-fetch-
    /// then-dictionary throughout (same house style as GetActiveAdmissionsHandler) so the round-trip
    /// count stays flat regardless of how many patients are on the ward, instead of calling the
    /// single-admission GetMarGridHandler in a per-patient loop. MAR due/overdue tallying reuses
    /// MarSlotMatcher/MarScheduleCalculator so this never silently disagrees with the MAR grid
    /// itself for the same patient.
    /// </summary>
    public class GetNursingStationSummaryHandler : IRequestHandler<GetNursingStationSummaryRequestModel, GetNursingStationSummaryResponseModel>
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        private readonly AppDbContext _context;

        public GetNursingStationSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNursingStationSummaryResponseModel> Handle(GetNursingStationSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetNursingStationSummaryResponseModel { Success = false, Message = "HospitalId is required." };

                var nurseUserId = request.NurseUserId;
                if (nurseUserId == null || nurseUserId == Guid.Empty)
                    return new GetNursingStationSummaryResponseModel { Success = false, Message = "NurseUserId is required." };

                var nurseName = await _context.UserProfiles
                    .Where(up => up.UserID == nurseUserId.Value)
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => up.FullName)
                    .FirstOrDefaultAsync(cancellationToken);

                // Step 1: what is this nurse rostered to, right now.
                var todayIst = (DateTime.UtcNow + IstOffset).Date;
                var rosterQuery = _context.NurseShiftAssignment.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId
                        && a.NurseUserId == nurseUserId.Value
                        && a.StatusCode == IpdConstants.NurseAssignmentStatus.Active
                        && (a.ShiftDate == null || a.ShiftDate == todayIst));

                if (!string.IsNullOrWhiteSpace(request.WardCode))
                    rosterQuery = rosterQuery.Where(a => a.WardCode == request.WardCode);
                if (!string.IsNullOrWhiteSpace(request.ShiftCode))
                {
                    var shiftCode = request.ShiftCode.Trim().ToUpperInvariant();
                    rosterQuery = rosterQuery.Where(a => a.ShiftCode == shiftCode);
                }

                var roster = await rosterQuery.ToListAsync(cancellationToken);
                if (roster.Count == 0)
                {
                    return new GetNursingStationSummaryResponseModel { Success = true, NurseName = nurseName, HasAssignments = false };
                }

                var wardCodes = roster.Select(r => r.WardCode).Distinct().ToList();

                // Step 3: the beds that live on those wards.
                var beds = await _context.BedMaster.AsNoTracking()
                    .Where(b => b.HospitalId == request.HospitalId && b.WardCode != null && wardCodes.Contains(b.WardCode!))
                    .ToListAsync(cancellationToken);
                var bedsById = beds.ToDictionary(b => b.BedId);
                var bedIds = beds.Select(b => b.BedId).ToList();

                // Step 4: who currently occupies those beds.
                var activeBedAssignments = await _context.BedAssignment.AsNoTracking()
                    .Where(b => b.HospitalId == request.HospitalId && b.StatusCode == IpdConstants.BedAssignmentStatus.Active && bedIds.Contains(b.BedId))
                    .ToListAsync(cancellationToken);
                var bedByAdmission = activeBedAssignments.ToDictionary(b => b.AdmissionId, b => bedsById[b.BedId]);

                if (bedByAdmission.Count == 0)
                {
                    return new GetNursingStationSummaryResponseModel { Success = true, NurseName = nurseName, HasAssignments = true };
                }

                // Step 5: only admissions still genuinely active (a lagging bed release must not
                // resurrect a discharged patient on the station).
                var candidateAdmissionIds = bedByAdmission.Keys.ToList();
                var admissions = await _context.Admission.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId
                        && candidateAdmissionIds.Contains(a.AdmissionId)
                        && IpdConstants.AdmissionStatus.Active.Contains(a.StatusCode))
                    .ToListAsync(cancellationToken);

                if (admissions.Count == 0)
                {
                    return new GetNursingStationSummaryResponseModel { Success = true, NurseName = nurseName, HasAssignments = true };
                }

                var admissionIds = admissions.Select(a => a.AdmissionId).ToList();

                // Per-patient nurse assignment (PatientNurseAssignment), independent of the ward
                // roster this board is otherwise driven by -- same two-step bulk-fetch-then-
                // dictionary idiom as the ward roster's own nurse-name lookup.
                var patientAssignments = await _context.PatientNurseAssignment.AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId
                        && admissionIds.Contains(p.AdmissionId)
                        && p.StatusCode == IpdConstants.NurseAssignmentStatus.Active
                        && (p.ShiftDate == null || p.ShiftDate == todayIst))
                    .ToListAsync(cancellationToken);

                var assignedNurseUserIds = patientAssignments.Select(p => p.NurseUserId).Distinct().ToList();
                var assignedNurseProfiles = await _context.UserProfiles.AsNoTracking()
                    .Where(up => assignedNurseUserIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .ToListAsync(cancellationToken);
                var assignedNameByUser = assignedNurseProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

                var assignedNurseNamesByAdmission = patientAssignments
                    .GroupBy(p => p.AdmissionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(p => assignedNameByUser.TryGetValue(p.NurseUserId, out var n) ? n : null).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList());

                // Step 6: patient demographics.
                var patientIds = admissions.Select(a => a.PatientId).Distinct().ToList();
                var patientsById = await _context.PatientRegistrations.AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId!))
                    .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

                // Step 7: admitting-doctor name, same Doctors -> UserProfiles join GetActiveAdmissionsHandler uses.
                var doctorIds = admissions.Where(a => a.PrimaryDoctorId.HasValue).Select(a => a.PrimaryDoctorId!.Value).Distinct().ToList();
                var doctorUserIds = await _context.Doctors.AsNoTracking()
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Select(d => new { d.DoctorID, d.UserID })
                    .ToListAsync(cancellationToken);
                var doctorProfileUserIds = doctorUserIds.Select(d => d.UserID).Distinct().ToList();
                var doctorProfiles = await _context.UserProfiles.AsNoTracking()
                    .Where(up => doctorProfileUserIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .ToListAsync(cancellationToken);
                var nameByUser = doctorProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);
                var doctorNameById = doctorUserIds.ToDictionary(d => d.DoctorID, d => nameByUser.TryGetValue(d.UserID, out var n) ? n : null);

                // Step 8: latest vitals within the last 48h, bulk-fetched then folded (same
                // OrderByDescending + GroupBy-first "latest wins" idiom GetActiveAdmissionsHandler
                // uses for AdmissionCoverage). No vitals in 48h => LastVitalAt stays null (stale flag).
                var now = DateTime.UtcNow;
                var vitalRows = await _context.VitalReading.AsNoTracking()
                    .Where(v => v.HospitalId == request.HospitalId && admissionIds.Contains(v.AdmissionId) && v.RecordedAt >= now.AddHours(-48))
                    .OrderByDescending(v => v.RecordedAt)
                    .ToListAsync(cancellationToken);
                var latestVitalByAdmission = vitalRows.GroupBy(v => v.AdmissionId).ToDictionary(g => g.Key, g => g.First());

                // Step 9: MAR, three queries total regardless of patient count.
                var orders = await _context.ClinicalOrder.AsNoTracking()
                    .Where(o => o.HospitalId == request.HospitalId && admissionIds.Contains(o.AdmissionId) && o.OrderType == IpdConstants.ClinicalOrderType.Medication)
                    .ToListAsync(cancellationToken);
                var ordersById = orders.ToDictionary(o => o.OrderId);
                var orderIds = orders.Select(o => o.OrderId).ToList();

                var windowStart = now - MarScheduleCalculator.MissedThreshold;
                var windowEnd = now.AddHours(4);

                var lines = await _context.ClinicalOrderLine.AsNoTracking()
                    .Where(l => orderIds.Contains(l.OrderId) && (l.StatusCode == IpdConstants.ClinicalOrderLineStatus.Active || l.UpdatedAt >= windowStart))
                    .ToListAsync(cancellationToken);

                var lineIds = lines.Select(l => l.OrderLineId).ToList();
                var fetchStart = windowStart - MarScheduleCalculator.MatchTolerance;
                var fetchEnd = windowEnd + MarScheduleCalculator.MatchTolerance;
                var administrations = await _context.MedicationAdministration.AsNoTracking()
                    .Where(m => m.HospitalId == request.HospitalId && m.OrderLineId != null && lineIds.Contains(m.OrderLineId.Value)
                        && m.ScheduledFor >= fetchStart && m.ScheduledFor <= fetchEnd)
                    .OrderByDescending(m => m.ActedAt)
                    .ToListAsync(cancellationToken);
                var adminsByLine = administrations.GroupBy(a => a.OrderLineId!.Value).ToDictionary(g => g.Key, g => g.ToList());

                var linesByAdmission = lines.GroupBy(l => ordersById[l.OrderId].AdmissionId).ToDictionary(g => g.Key, g => g.ToList());

                var dueByAdmission = new Dictionary<Guid, int>();
                var overdueByAdmission = new Dictionary<Guid, int>();
                var nextDoseByAdmission = new Dictionary<Guid, DateTime>();

                foreach (var admissionId in admissionIds)
                {
                    if (!linesByAdmission.TryGetValue(admissionId, out var admissionLines)) continue;

                    foreach (var line in admissionLines)
                    {
                        var order = ordersById[line.OrderId];
                        var computedSlots = MarScheduleCalculator.ComputeSlots(line.Frequency, order.OrderedAt, line.DurationDays, windowStart, windowEnd);
                        if (computedSlots.Count == 0) continue;

                        var candidateAdmins = adminsByLine.TryGetValue(line.OrderLineId, out var admins) ? admins : new List<MedicationAdministration>();
                        var slotMatches = MarSlotMatcher.Match(
                            computedSlots,
                            candidateAdmins.Select(m => new MarSlotMatcher.Candidate(m.MedicationAdministrationId, m.ScheduledFor, m.ActedAt)));

                        foreach (var slotMatch in slotMatches)
                        {
                            if (slotMatch.MatchedId.HasValue) continue;   // already acted on -- not due

                            var status = MarScheduleCalculator.DeriveSlotStatus(slotMatch.ScheduledForUtc, now);
                            if (status == IpdConstants.MarSlotStatus.Due)
                            {
                                dueByAdmission[admissionId] = dueByAdmission.GetValueOrDefault(admissionId) + 1;
                            }
                            else if (status == IpdConstants.MarSlotStatus.Overdue || status == IpdConstants.MarSlotStatus.Missed)
                            {
                                overdueByAdmission[admissionId] = overdueByAdmission.GetValueOrDefault(admissionId) + 1;
                            }

                            if (status == IpdConstants.MarSlotStatus.Due || status == IpdConstants.MarSlotStatus.Pending)
                            {
                                if (!nextDoseByAdmission.TryGetValue(admissionId, out var existing) || slotMatch.ScheduledForUtc < existing)
                                    nextDoseByAdmission[admissionId] = slotMatch.ScheduledForUtc;
                            }
                        }
                    }
                }

                // Step 10: one in-memory project, ordered by ward then bed.
                var items = admissions.Select(a =>
                {
                    var bed = bedByAdmission.TryGetValue(a.AdmissionId, out var b) ? b : null;
                    patientsById.TryGetValue(a.PatientId, out var patient);
                    latestVitalByAdmission.TryGetValue(a.AdmissionId, out var vital);

                    return new NursingStationPatientItem
                    {
                        AdmissionId = a.AdmissionId,
                        PatientId = a.PatientId,
                        PatientName = patient?.FullName,
                        PatientAge = patient?.Age,
                        PatientSex = patient?.Sex,
                        BedCode = bed?.BedCode,
                        WardCode = bed?.WardCode ?? string.Empty,
                        WardName = bed?.WardName,
                        PrimaryDoctorName = a.PrimaryDoctorId.HasValue && doctorNameById.TryGetValue(a.PrimaryDoctorId.Value, out var dn) ? dn : null,
                        LastVitalAt = vital?.RecordedAt,
                        LastPulse = vital?.Pulse,
                        LastSystolicBP = vital?.SystolicBP,
                        LastDiastolicBP = vital?.DiastolicBP,
                        LastTemperature = vital?.Temperature,
                        LastSpO2 = vital?.SpO2,
                        MedsDueCount = dueByAdmission.GetValueOrDefault(a.AdmissionId),
                        MedsOverdueCount = overdueByAdmission.GetValueOrDefault(a.AdmissionId),
                        NextDoseAtUtc = nextDoseByAdmission.TryGetValue(a.AdmissionId, out var next) ? next : null,
                        AssignedNurseNames = assignedNurseNamesByAdmission.TryGetValue(a.AdmissionId, out var assignedNames) ? assignedNames : new List<string>(),
                    };
                })
                .OrderBy(i => i.WardName ?? i.WardCode)
                .ThenBy(i => i.BedCode)
                .ToList();

                return new GetNursingStationSummaryResponseModel
                {
                    Success = true,
                    NurseName = nurseName,
                    HasAssignments = true,
                    TotalPatients = items.Count,
                    TotalMedsDue = items.Sum(i => i.MedsDueCount),
                    TotalMedsOverdue = items.Sum(i => i.MedsOverdueCount),
                    Items = items,
                };
            }
            catch (Exception)
            {
                return new GetNursingStationSummaryResponseModel { Success = false, Message = "Error loading the nursing station summary." };
            }
        }
    }
}
