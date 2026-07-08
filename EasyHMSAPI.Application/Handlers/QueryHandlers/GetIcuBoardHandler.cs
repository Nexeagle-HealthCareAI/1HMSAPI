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
            var patientIds = activeAdmissions.Select(a => a.PatientId).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId, cancellationToken);
                
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
                
            var icuCases = new List<IcuBoardCaseDataModel>();
            
            foreach (var a in activeAdmissions)
            {
                latestLevelOfCare.TryGetValue(a.AdmissionId, out var levelOfCare);
                var activeBed = activeBeds.FirstOrDefault(ba => ba.AdmissionId == a.AdmissionId);
                EasyHMSAPI.Domain.Entities.BedMaster? bed = null;
                if (activeBed != null) beds.TryGetValue(activeBed.BedId, out bed);
                
                // Determine if they are an ICU patient
                var isIcuWard = bed != null && (bed.WardType?.Contains("ICU") == true || bed.WardType?.Contains("CCU") == true);
                var hasIcuLevel = levelOfCare != null;
                
                if (!isIcuWard && !hasIcuLevel) continue; // Skip non-ICU patients
                
                patients.TryGetValue(a.PatientId, out var patient);
                latestApache.TryGetValue(a.AdmissionId, out var apache);
                latestSofa.TryGetValue(a.AdmissionId, out var sofa);

                icuCases.Add(new IcuBoardCaseDataModel
                {
                    AdmissionId = a.AdmissionId,
                    EncounterId = a.EncounterId ?? a.AdmissionId,
                    PatientName = patient?.FullName ?? "Unknown",
                    BedCode = bed?.BedCode,
                    WardCode = bed?.WardCode,
                    IcuLevel = levelOfCare?.Level,
                    ApacheScore = apache?.TotalScore,
                    SofaScore = sofa?.TotalScore,
                    OnVentilator = sofa?.OnRespiratorySupport ?? false,
                    PrimaryDiagnosis = a.ProvisionalDiagnosis // or admitting diagnosis
                });
            }
            
            return new GetIcuBoardResponseModel { Cases = icuCases };
        }
    }
}
