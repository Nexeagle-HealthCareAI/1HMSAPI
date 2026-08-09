using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetIcuBoardHandler : IRequestHandler<GetIcuBoardRequestModel, GetIcuBoardResponseModel>
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        private readonly AppDbContext _context;

        public GetIcuBoardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetIcuBoardResponseModel> Handle(GetIcuBoardRequestModel request, CancellationToken cancellationToken)
        {
            // 1. Get active admissions that are in ICU.
            // ICU patients are defined by either having an active ICU level of care, OR occupying a bed in an ICU ward.
            // For now, let's look at all active admissions and their latest level of care.
            
            var activeAdmissions = await _context.Admission
                .Where(a => a.HospitalId == request.HospitalId && IpdConstants.AdmissionStatus.Active.Contains(a.StatusCode))
                .ToListAsync(cancellationToken);
                
            var admissionIds = activeAdmissions.Select(a => a.AdmissionId).ToList();
            if (admissionIds.Count == 0) return new GetIcuBoardResponseModel();

            // Active bed assignments
            var activeBeds = await _context.BedAssignment
                .Where(ba => admissionIds.Contains(ba.AdmissionId) && ba.StatusCode == IpdConstants.BedAssignmentStatus.Active)
                .ToListAsync(cancellationToken);
                
            var bedIds = activeBeds.Select(ba => ba.BedId).Distinct().ToList();
            var beds = await _context.BedMaster
                .Where(b => bedIds.Contains(b.BedId))
                .ToDictionaryAsync(b => b.BedId, cancellationToken);
                
            // Fetch patients
            var patientIds = activeAdmissions.Select(a => a.PatientId).Where(id => id != null).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId!, cancellationToken);
                
            // Latest Level of Care
            var latestLevelOfCare = await _context.IcuLevelOfCare
                .Where(l => admissionIds.Contains(l.AdmissionId))
                .GroupBy(l => l.AdmissionId)
                .Select(g => g.OrderByDescending(x => x.AssessedAt).First())
                .ToDictionaryAsync(l => l.AdmissionId, cancellationToken);
                
            // Latest Apache
            var latestApache = await _context.ApacheIIScore
                .Where(s => admissionIds.Contains(s.AdmissionId))
                .GroupBy(s => s.AdmissionId)
                .Select(g => g.OrderByDescending(x => x.ScoredAt).First())
                .ToDictionaryAsync(s => s.AdmissionId, cancellationToken);
                
            // Latest Sofa
            var latestSofa = await _context.SofaScore
                .Where(s => admissionIds.Contains(s.AdmissionId))
                .GroupBy(s => s.AdmissionId)
                .Select(g => g.OrderByDescending(x => x.ScoredAt).First())
                .ToDictionaryAsync(s => s.AdmissionId, cancellationToken);

            // Latest ventilator settings -- a real record here wins over the SofaScore proxy below.
            var latestVentilator = await _context.VentilatorSettings
                .Where(v => admissionIds.Contains(v.AdmissionId))
                .GroupBy(v => v.AdmissionId)
                .Select(g => g.OrderByDescending(x => x.ScoredAt).First())
                .ToDictionaryAsync(v => v.AdmissionId, cancellationToken);

            // Latest Early Warning Score
            var latestEws = await _context.EarlyWarningScore
                .Where(s => admissionIds.Contains(s.AdmissionId))
                .GroupBy(s => s.AdmissionId)
                .Select(g => g.OrderByDescending(x => x.ScoredAt).First())
                .ToDictionaryAsync(s => s.AdmissionId, cancellationToken);

            // Nurse roster per ward, ward-level grain (no per-bed assignment exists) -- same
            // NurseShiftAssignment/UserProfiles two-step join GetNursingStationSummaryHandler uses.
            var todayIst = (DateTime.UtcNow + IstOffset).Date;
            var wardCodes = beds.Values.Where(b => b.WardCode != null).Select(b => b.WardCode!).Distinct().ToList();
            var nurseNamesByWard = new Dictionary<string, List<string>>();
            if (wardCodes.Count > 0)
            {
                var roster = await _context.NurseShiftAssignment
                    .Where(n => n.HospitalId == request.HospitalId
                        && wardCodes.Contains(n.WardCode)
                        && n.StatusCode == IpdConstants.NurseAssignmentStatus.Active
                        && (n.ShiftDate == null || n.ShiftDate == todayIst))
                    .ToListAsync(cancellationToken);

                var nurseUserIds = roster.Select(r => r.NurseUserId).Distinct().ToList();
                var nurseProfiles = await _context.UserProfiles
                    .Where(up => nurseUserIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .ToListAsync(cancellationToken);
                var nameByUser = nurseProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

                nurseNamesByWard = roster
                    .GroupBy(r => r.WardCode)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(r => nameByUser.TryGetValue(r.NurseUserId, out var n) ? n : null).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList());
            }

            // Real per-patient assignment (PatientNurseAssignment), independent of the ward-level
            // roster above -- same two-step bulk-fetch-then-dictionary idiom, just keyed by
            // AdmissionId instead of WardCode.
            var patientAssignments = await _context.PatientNurseAssignment
                .Where(p => p.HospitalId == request.HospitalId
                    && admissionIds.Contains(p.AdmissionId)
                    && p.StatusCode == IpdConstants.NurseAssignmentStatus.Active
                    && (p.ShiftDate == null || p.ShiftDate == todayIst))
                .ToListAsync(cancellationToken);

            var assignedNurseUserIds = patientAssignments.Select(p => p.NurseUserId).Distinct().ToList();
            var assignedNurseProfiles = await _context.UserProfiles
                .Where(up => assignedNurseUserIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .ToListAsync(cancellationToken);
            var assignedNameByUser = assignedNurseProfiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

            var assignedNurseNamesByAdmission = patientAssignments
                .GroupBy(p => p.AdmissionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => assignedNameByUser.TryGetValue(p.NurseUserId, out var n) ? n : null).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList());

            // Latest vitals within the last 4h -- anything older is already "stale" for ICU purposes.
            var vitalsSince = DateTime.UtcNow.AddHours(-4);
            var vitalRows = await _context.VitalReading
                .Where(v => v.HospitalId == request.HospitalId && admissionIds.Contains(v.AdmissionId) && v.RecordedAt >= vitalsSince)
                .OrderByDescending(v => v.RecordedAt)
                .ToListAsync(cancellationToken);
            var latestVitalByAdmission = vitalRows.GroupBy(v => v.AdmissionId).ToDictionary(g => g.Key, g => g.First());

            // Admissions with a currently-open Rapid Response activation
            var openRrtAdmissionIds = (await _context.RapidResponseActivation
                    .Where(r => admissionIds.Contains(r.AdmissionId) && r.ResolvedAt == null)
                    .Select(r => r.AdmissionId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            // Active devices (central line/catheter/ETT) per admission, plus each device's latest
            // bundle-compliance check time -- a device is "overdue" once >24h have passed since its
            // last check (or since insertion, if never checked). Plain UTC comparison, no timezone math.
            var activeDevices = await _context.DeviceAssignment
                .Where(d => admissionIds.Contains(d.AdmissionId) && d.StatusCode == IpdConstants.DeviceStatus.Active)
                .ToListAsync(cancellationToken);

            var activeDeviceIds = activeDevices.Select(d => d.DeviceAssignmentId).ToList();
            var latestCheckByDevice = await _context.DeviceCareBundleCheck
                .Where(c => activeDeviceIds.Contains(c.DeviceAssignmentId))
                .GroupBy(c => c.DeviceAssignmentId)
                .Select(g => g.OrderByDescending(x => x.CheckedAt).First())
                .ToDictionaryAsync(c => c.DeviceAssignmentId, cancellationToken);

            var nowForBundleCheck = DateTime.UtcNow;
            var overdueAdmissionIds = activeDevices
                .Where(d =>
                {
                    var lastChecked = latestCheckByDevice.TryGetValue(d.DeviceAssignmentId, out var check) ? check.CheckedAt : d.InsertedAt;
                    return nowForBundleCheck - lastChecked > TimeSpan.FromHours(24);
                })
                .Select(d => d.AdmissionId)
                .ToHashSet();

            var icuCases = new List<IcuBoardCaseDataModel>();
            
            foreach (var a in activeAdmissions)
            {
                latestLevelOfCare.TryGetValue(a.AdmissionId, out var levelOfCare);
                var activeBed = activeBeds.FirstOrDefault(ba => ba.AdmissionId == a.AdmissionId);
                EasyHMSAPI.Domain.Entities.BedMaster? bed = null;
                if (activeBed != null) beds.TryGetValue(activeBed.BedId, out bed);
                
                // Determine if they are an ICU patient
                var isIcuWard = IpdConstants.WardType.IsIcuFamily(bed?.WardType);
                var hasIcuLevel = levelOfCare != null;
                
                if (!isIcuWard && !hasIcuLevel) continue; // Skip non-ICU patients
                
                EasyHMSAPI.Domain.Entities.PatientRegistration? patient = null;
                if (a.PatientId != null) patients.TryGetValue(a.PatientId, out patient);
                
                latestApache.TryGetValue(a.AdmissionId, out var apache);
                latestSofa.TryGetValue(a.AdmissionId, out var sofa);
                latestEws.TryGetValue(a.AdmissionId, out var ews);
                latestVitalByAdmission.TryGetValue(a.AdmissionId, out var vital);
                var nurseNames = bed?.WardCode != null && nurseNamesByWard.TryGetValue(bed.WardCode, out var names) ? names : new List<string>();

                icuCases.Add(new IcuBoardCaseDataModel
                {
                    AdmissionId = a.AdmissionId,
                    EncounterId = a.EncounterId ?? a.AdmissionId,
                    PatientId = a.PatientId,
                    PatientName = patient?.FullName ?? "Unknown",
                    BedCode = bed?.BedCode,
                    WardCode = bed?.WardCode,
                    IcuLevel = levelOfCare?.Level,
                    ApacheScore = apache?.TotalScore,
                    SofaScore = sofa?.TotalScore,
                    // A real ventilator record wins; the SOFA checkbox is only a fallback proxy
                    // for admissions that have never had a ventilator setting recorded.
                    OnVentilator = latestVentilator.ContainsKey(a.AdmissionId) || (sofa?.OnRespiratorySupport ?? false),
                    PrimaryDiagnosis = a.Diagnosis,
                    EwsScore = ews?.TotalScore,
                    EwsRiskBand = ews?.RiskBand,
                    HasOpenRapidResponse = openRrtAdmissionIds.Contains(a.AdmissionId),
                    ActiveDeviceCount = activeDevices.Count(d => d.AdmissionId == a.AdmissionId),
                    HasOverdueBundleCheck = overdueAdmissionIds.Contains(a.AdmissionId),
                    NurseNames = nurseNames,
                    AssignedNurseNames = assignedNurseNamesByAdmission.TryGetValue(a.AdmissionId, out var assignedNames) ? assignedNames : new List<string>(),
                    LastVitalAt = vital?.RecordedAt,
                    LastPulse = vital?.Pulse,
                    LastSystolicBP = vital?.SystolicBP,
                    LastDiastolicBP = vital?.DiastolicBP,
                    LastTemperature = vital?.Temperature,
                    LastSpO2 = vital?.SpO2,
                });
            }
            
            return new GetIcuBoardResponseModel { Cases = icuCases };
        }
    }
}
