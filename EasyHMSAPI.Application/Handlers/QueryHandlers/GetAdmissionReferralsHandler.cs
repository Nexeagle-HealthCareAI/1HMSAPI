using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
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
                // Every filter except StatusCode -- reused both for the paginated listing (with
                // StatusCode applied on top) and for the StatusCounts breakdown (without it, so
                // switching status chips never hides sibling counts -- same "faceted filter" UX as
                // email inbox category tabs).
                var baseQuery = _context.AdmissionReferrals
                    .Where(r => r.HospitalId == request.HospitalId);

                if (!string.IsNullOrWhiteSpace(request.PatientId))
                    baseQuery = baseQuery.Where(r => r.PatientId == request.PatientId);

                if (!string.IsNullOrWhiteSpace(request.CaseType))
                    baseQuery = baseQuery.Where(r => r.CaseType == request.CaseType);

                if (request.ReferringDoctorId.HasValue && request.ReferringDoctorId != Guid.Empty)
                    baseQuery = baseQuery.Where(r => r.ReferringDoctorId == request.ReferringDoctorId);

                if (request.FromDate.HasValue)
                    baseQuery = baseQuery.Where(r => r.CreatedAt >= request.FromDate.Value);

                if (request.ToDate.HasValue)
                    baseQuery = baseQuery.Where(r => r.CreatedAt <= request.ToDate.Value);

                var statusCounts = IpdConstants.ReferralStatus.All
                    .ToDictionary(s => s, _ => 0);
                var realCounts = await baseQuery
                    .GroupBy(r => r.StatusCode)
                    .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken);
                foreach (var c in realCounts)
                    statusCounts[c.StatusCode] = c.Count;

                var query = baseQuery;
                if (!string.IsNullOrWhiteSpace(request.StatusCode))
                    query = query.Where(r => r.StatusCode == request.StatusCode);

                var totalCount = await query.CountAsync(cancellationToken);

                var referrals = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
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

                var appointmentIds = referrals.Where(r => r.AppointmentId.HasValue).Select(r => r.AppointmentId!.Value).Distinct().ToList();
                var apptStatusById = await _context.Appointments
                    .Where(a => appointmentIds.Contains(a.ApptId))
                    .ToDictionaryAsync(a => a.ApptId, a => a.CurrentStatusCode, cancellationToken);

                var convertedAdmissionIds = referrals.Where(r => r.ConvertedAdmissionId.HasValue).Select(r => r.ConvertedAdmissionId!.Value).Distinct().ToList();
                var admittedAtById = await _context.Admission
                    .Where(a => convertedAdmissionIds.Contains(a.AdmissionId))
                    .ToDictionaryAsync(a => a.AdmissionId, a => a.AdmittedAt, cancellationToken);

                // Comment count for this page only -- avoids the frontend needing a second round trip
                // just to know whether a card should show a comment badge; full text lazy-loads on toggle.
                var referralIds = referrals.Select(r => r.ReferralId).ToList();
                var commentCountByReferral = await _context.AdmissionReferralComment
                    .Where(c => referralIds.Contains(c.ReferralId))
                    .GroupBy(c => c.ReferralId)
                    .Select(g => new { ReferralId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.ReferralId, g => g.Count, cancellationToken);

                response.Referrals = referrals.Select(r => new AdmissionReferralDataModel
                {
                    ReferralId = r.ReferralId,
                    PatientId = r.PatientId,
                    PatientName = patientByPatientId.TryGetValue(r.PatientId, out var pat) ? pat.FullName : null,
                    PatientMobile = patientByPatientId.TryGetValue(r.PatientId, out var pat2) ? pat2.Mobile : null,
                    ReferringDoctorId = r.ReferringDoctorId,
                    ReferringDoctorName = doctorNameById.TryGetValue(r.ReferringDoctorId, out var dn) ? dn : null,
                    AppointmentId = r.AppointmentId,
                    SourceAppointmentCancelled = r.StatusCode == "PENDING" && r.AppointmentId.HasValue
                        && apptStatusById.TryGetValue(r.AppointmentId.Value, out var apptStatus) && apptStatus == AppConstants.AppointmentStatus_Cancelled,
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
                    AdmittedAt = r.ConvertedAdmissionId.HasValue && admittedAtById.TryGetValue(r.ConvertedAdmissionId.Value, out var admittedAt) ? admittedAt : null,
                    CreatedAt = r.CreatedAt,
                    CommentCount = commentCountByReferral.TryGetValue(r.ReferralId, out var cc) ? cc : 0,
                }).ToList();
                response.Page = request.Page;
                response.PageSize = request.PageSize;
                response.TotalCount = totalCount;
                response.StatusCounts = statusCounts.Select(kv => new ReferralStatusCountItem { StatusCode = kv.Key, Count = kv.Value }).ToList();
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
