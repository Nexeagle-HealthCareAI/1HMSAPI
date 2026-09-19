using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) specialty-category list — same platform-wide, IsPubliclyListed-gated
    /// filtering as GetPublicDoctorsHandler, so a category only appears here when a patient could
    /// actually book a doctor in it via GET /public/doctors?specialtyCategory=... . Exists so callers
    /// (e.g. the WhatsApp booking bot) don't have to page through every doctor and group client-side
    /// just to build a department/specialty menu.
    /// A doctor's category prefers the normalized MedicalSpecialities.PatientFacingCategory
    /// (Doctor.PrimaryMedicalSpecialityId), but that link is optional and admin-set — nothing
    /// requires it to be filled in when a doctor is onboarded. A doctor whose link is missing or
    /// points at an inactive/incomplete row falls back to their plain Department name instead of
    /// being dropped entirely (see bug: prod doctors mostly had Department set but not
    /// PrimaryMedicalSpecialityId, so this endpoint returned only 2 of ~11 real categories).
    /// </summary>
    public class GetPublicSpecialtiesHandler : IRequestHandler<GetPublicSpecialtiesRequestModel, GetPublicSpecialtiesResponseModel>
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public GetPublicSpecialtiesHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<GetPublicSpecialtiesResponseModel> Handle(GetPublicSpecialtiesRequestModel request, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(PublicDirectoryCacheKeys.PublicSpecialtiesList, out GetPublicSpecialtiesResponseModel? cached) && cached != null)
            {
                return cached;
            }

            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var activeHospitalIds = await _context.Hospitals
                .Where(h => h.IsActive && !h.IsArchived)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            if (activeHospitalIds.Count == 0)
            {
                return EmptyResult();
            }

            // Same eligibility rule as GetPublicDoctorsHandler: a hospital counts if it opted in
            // itself, OR it has at least one CMS-force-listed doctor.
            var selfListedIds = await _context.Hospitals
                .Where(h => activeHospitalIds.Contains(h.HospitalID) && h.IsPubliclyListed)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            var forcedListingHospitalIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue && activeHospitalIds.Contains(dd.HospitalId!.Value))
                .Join(_context.Doctors.Where(d => d.IsPubliclyListed && !d.IsDelistedByAdmin),
                      dd => dd.DoctorID, d => d.DoctorID, (dd, d) => dd.HospitalId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var publicHospitalIds = selfListedIds.Union(forcedListingHospitalIds).ToList();

            if (publicHospitalIds.Count == 0)
            {
                return EmptyResult();
            }

            var eligibleDoctorIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue && publicHospitalIds.Contains(dd.HospitalId!.Value))
                .Select(dd => dd.DoctorID)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (eligibleDoctorIds.Count == 0)
            {
                return EmptyResult();
            }

            var doctorRows = await (
                from d in _context.Doctors
                where eligibleDoctorIds.Contains(d.DoctorID) && d.IsPubliclyListed && !d.IsDelistedByAdmin
                join u in _context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                select new { d.DoctorID, d.PrimaryMedicalSpecialityId, d.PrimaryDepartmentID })
                .ToListAsync(cancellationToken);

            if (doctorRows.Count == 0)
            {
                return EmptyResult();
            }

            var specialityIds = doctorRows
                .Where(r => r.PrimaryMedicalSpecialityId.HasValue)
                .Select(r => r.PrimaryMedicalSpecialityId!.Value)
                .Distinct()
                .ToList();

            var specialityById = await _context.MedicalSpecialities
                .Where(ms => specialityIds.Contains(ms.SpecialityId) && ms.IsActive && ms.PatientFacingCategory != null)
                .Select(ms => new { ms.SpecialityId, ms.PatientFacingCategory, ms.PatientFacingName })
                .ToDictionaryAsync(ms => ms.SpecialityId, cancellationToken);

            var departmentIds = doctorRows
                .Where(r => r.PrimaryDepartmentID.HasValue)
                .Select(r => r.PrimaryDepartmentID!.Value)
                .Distinct()
                .ToList();

            var departmentNameById = await _context.Departments
                .Where(dept => departmentIds.Contains(dept.DepartmentID) && dept.IsActive)
                .Select(dept => new { dept.DepartmentID, dept.Name })
                .ToDictionaryAsync(dept => dept.DepartmentID, dept => dept.Name, cancellationToken);

            // A doctor's category is their normalized MedicalSpecialities.PatientFacingCategory when
            // that (optional, admin-set) link is populated and still active; otherwise fall back to
            // their Department name so a doctor with a department but no speciality mapping doesn't
            // silently disappear from this list. GetPublicDoctorsHandler's SpecialtyCategory filter
            // applies the identical fallback, so a Category returned here always round-trips there.
            var categories = doctorRows
                .Select(r =>
                {
                    if (r.PrimaryMedicalSpecialityId.HasValue &&
                        specialityById.TryGetValue(r.PrimaryMedicalSpecialityId.Value, out var speciality))
                    {
                        return (Category: speciality.PatientFacingCategory, DisplayName: speciality.PatientFacingName ?? speciality.PatientFacingCategory);
                    }

                    if (r.PrimaryDepartmentID.HasValue &&
                        departmentNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var deptName))
                    {
                        return (Category: deptName, DisplayName: deptName)!;
                    }

                    return (Category: null, DisplayName: null);
                })
                .Where(x => x.Category != null)
                .GroupBy(x => x.Category!)
                .Select(g => new PublicSpecialtyInfo
                {
                    Category = g.Key,
                    DisplayName = g.First().DisplayName ?? g.Key,
                    DoctorCount = g.Count(),
                })
                .OrderByDescending(c => c.DoctorCount)
                .ThenBy(c => c.Category)
                .ToList();

            var response = new GetPublicSpecialtiesResponseModel { Success = true, Specialties = categories };
            _cache.Set(PublicDirectoryCacheKeys.PublicSpecialtiesList, response, CacheTtl);
            return response;
        }

        private static GetPublicSpecialtiesResponseModel EmptyResult() =>
            new() { Success = true, Specialties = new() };
    }
}
