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

            var categories = await (
                from d in _context.Doctors
                where eligibleDoctorIds.Contains(d.DoctorID) && d.IsPubliclyListed && !d.IsDelistedByAdmin
                      && d.PrimaryMedicalSpecialityId != null
                join u in _context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                join ms in _context.MedicalSpecialities on d.PrimaryMedicalSpecialityId equals ms.SpecialityId
                where ms.IsActive && ms.PatientFacingCategory != null
                group ms by ms.PatientFacingCategory into g
                select new PublicSpecialtyInfo
                {
                    Category = g.Key!,
                    DisplayName = g.Select(x => x.PatientFacingName).FirstOrDefault(n => n != null) ?? g.Key,
                    DoctorCount = g.Count(),
                })
                .OrderByDescending(c => c.DoctorCount)
                .ThenBy(c => c.Category)
                .ToListAsync(cancellationToken);

            var response = new GetPublicSpecialtiesResponseModel { Success = true, Specialties = categories };
            _cache.Set(PublicDirectoryCacheKeys.PublicSpecialtiesList, response, CacheTtl);
            return response;
        }

        private static GetPublicSpecialtiesResponseModel EmptyResult() =>
            new() { Success = true, Specialties = new() };
    }
}
