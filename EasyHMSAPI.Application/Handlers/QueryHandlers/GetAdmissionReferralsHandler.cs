using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAdmissionReferralsHandler : IRequestHandler<GetAdmissionReferralsRequestModel, GetAdmissionReferralsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionReferralsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionReferralsResponseModel> Handle(GetAdmissionReferralsRequestModel request, CancellationToken cancellationToken)
        {
            GetAdmissionReferralsResponseModel response = new() { Success = false };
            try
            {
                var query = _context.AdmissionReferrals
                    .Where(r => r.HospitalId == request.HospitalId);

                if (!string.IsNullOrWhiteSpace(request.StatusCode))
                    query = query.Where(r => r.StatusCode == request.StatusCode);

                if (!string.IsNullOrWhiteSpace(request.CaseType))
                    query = query.Where(r => r.CaseType == request.CaseType);

                if (request.ReferringDoctorId.HasValue && request.ReferringDoctorId != Guid.Empty)
                    query = query.Where(r => r.ReferringDoctorId == request.ReferringDoctorId);

                if (request.FromDate.HasValue)
                    query = query.Where(r => r.CreatedAt >= request.FromDate.Value);

                if (request.ToDate.HasValue)
                    query = query.Where(r => r.CreatedAt <= request.ToDate.Value);

                var referrals = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(cancellationToken);

                var patientIds = referrals.Select(r => r.PatientId).Distinct().ToList();
                var patients = await _context.PatientRegistrations
                    .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId!))
                    .Select(p => new { p.PatientId, p.FullName, p.Mobile })
                    .ToListAsync(cancellationToken);
                var patientByPatientId = patients
                    .GroupBy(p => p.PatientId!)
                    .ToDictionary(g => g.Key, g => g.First());

                var doctorIds = referrals.Select(r => r.ReferringDoctorId).Distinct().ToList();
                var doctorUserIds = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Select(d => new { d.DoctorID, d.UserID })
                    .ToListAsync(cancellationToken);
                var userIds = doctorUserIds.Select(d => d.UserID).Distinct().ToList();
                var nameByUser = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameByUserLookup = nameByUser.GroupBy(n => n.UserID).ToDictionary(g => g.Key, g => g.First().FullName);
                var doctorNameById = doctorUserIds.ToDictionary(d => d.DoctorID, d => nameByUserLookup.TryGetValue(d.UserID, out var n) ? n : null);

                var planIds = referrals.Where(r => r.OtPlanId.HasValue).Select(r => r.OtPlanId!.Value).Distinct().ToList();
                var planNameById = await _context.OTPlans
                    .Where(p => planIds.Contains(p.OtPlanId))
                    .ToDictionaryAsync(p => p.OtPlanId, p => p.PlanName, cancellationToken);

                var packageTypeIds = referrals.Where(r => r.PackageTypeId.HasValue).Select(r => r.PackageTypeId!.Value).Distinct().ToList();
                var packageTypesById = await _context.PackageTypes
                    .Where(pt => packageTypeIds.Contains(pt.PackageTypeId))
                    .ToDictionaryAsync(pt => pt.PackageTypeId, pt => new { pt.Name, pt.Price }, cancellationToken);

                response.Referrals = referrals.Select(r => new AdmissionReferralDataModel
                {
                    ReferralId = r.ReferralId,
                    PatientId = r.PatientId,
                    PatientName = patientByPatientId.TryGetValue(r.PatientId, out var pat) ? pat.FullName : null,
                    PatientMobile = patientByPatientId.TryGetValue(r.PatientId, out var pat2) ? pat2.Mobile : null,
                    ReferringDoctorId = r.ReferringDoctorId,
                    ReferringDoctorName = doctorNameById.TryGetValue(r.ReferringDoctorId, out var dn) ? dn : null,
                    OtPlanId = r.OtPlanId,
                    OtPlanName = r.OtPlanId.HasValue && planNameById.TryGetValue(r.OtPlanId.Value, out var pn) ? pn : null,
                    PackageTypeId = r.PackageTypeId,
                    PackageTypeName = r.PackageTypeId.HasValue && packageTypesById.TryGetValue(r.PackageTypeId.Value, out var pkg) ? pkg.Name : null,
                    PackageTypePrice = r.PackageTypeId.HasValue && packageTypesById.TryGetValue(r.PackageTypeId.Value, out var pkg2) ? pkg2.Price : null,
                    ProcedureName = r.ProcedureName,
                    ProbableAdmissionDate = r.ProbableAdmissionDate,
                    CaseType = r.CaseType,
                    Notes = r.Notes,
                    StatusCode = r.StatusCode,
                    NotAdmittedReason = r.NotAdmittedReason,
                    FollowUpDate = r.FollowUpDate,
                    FollowUpNotes = r.FollowUpNotes,
                    ConvertedAdmissionId = r.ConvertedAdmissionId,
                    CreatedAt = r.CreatedAt,
                }).ToList();
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
