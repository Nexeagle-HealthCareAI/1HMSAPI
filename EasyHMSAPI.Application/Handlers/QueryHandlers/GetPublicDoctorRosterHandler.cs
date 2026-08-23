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
    /// Internal hospital-wide doctor roster on the PUBLIC surface (PublicController), gated only
    /// by knowing a real HospitalId (see PublicApiKeyFilter's doc comment: the X-Api-Key header
    /// carries no hospital identity/access control -- same trust model GetPublicDoctorsHandler's
    /// own HospitalId bypass already uses). Built for Vita's voice assistant to phonetically
    /// pre-correct a mis-transcribed doctor name before ever calling find_doctors -- not itself a
    /// tool-call result, so it's deliberately leaner than GetPublicDoctorsHandler /
    /// GetHospitalDoctorsHandler.
    ///
    /// Resolves doctors purely via DoctorDepartments membership at this hospital -- like
    /// GetHospitalDoctorsHandler, UNLIKE GetPublicDoctorsHandler, this never filters on
    /// Doctor.IsPubliclyListed or IsDelistedByAdmin: a hospital's own front desk needs every real
    /// doctor practicing there, whether or not that doctor has opted into (or been delisted from)
    /// the platform-wide marketplace -- those are marketing-surface concerns, orthogonal to
    /// internal usability. Still excludes a Revoked user account (mirrors GetPublicDoctorsHandler's
    /// own UserStatusId filter) so this roster never contains a name find_doctors itself could
    /// never subsequently resolve -- that mismatch would be worse than omitting the doctor.
    /// </summary>
    public class GetPublicDoctorRosterHandler : IRequestHandler<GetPublicDoctorRosterRequestModel, GetPublicDoctorRosterResponseModel>
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public GetPublicDoctorRosterHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<GetPublicDoctorRosterResponseModel> Handle(GetPublicDoctorRosterRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new GetPublicDoctorRosterResponseModel { Success = false, Message = "HospitalId is required." };

            var cacheKey = PublicDirectoryCacheKeys.DoctorRoster(request.HospitalId);
            if (_cache.TryGetValue(cacheKey, out GetPublicDoctorRosterResponseModel? cached) && cached != null)
                return cached;

            // Same "hospitalId bypasses IsPubliclyListed but still requires Active/not-archived"
            // posture GetPublicDoctorsRequestModel.HospitalId already documents.
            var hospitalExists = await _context.Hospitals
                .AnyAsync(h => h.HospitalID == request.HospitalId && h.IsActive && !h.IsArchived, cancellationToken);
            if (!hospitalExists)
                return new GetPublicDoctorRosterResponseModel { Success = false, Message = "Hospital not found or inactive." };

            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var doctorIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId == request.HospitalId)
                .Select(dd => dd.DoctorID)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (doctorIds.Count == 0)
            {
                var empty = new GetPublicDoctorRosterResponseModel { Success = true, Doctors = new() };
                _cache.Set(cacheKey, empty, CacheTtl);
                return empty;
            }

            var rows = await (
                from d in _context.Doctors
                where doctorIds.Contains(d.DoctorID)
                join u in _context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                select new { d.DoctorID, d.UserID, d.PrimaryDepartmentID, d.PrimaryMedicalSpecialityId }
            ).ToListAsync(cancellationToken);

            var userIds = rows.Select(r => r.UserID).Distinct().ToList();
            var nameByUser = await _context.UserProfiles
                .Where(up => userIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .Select(up => new { up.UserID, up.FullName })
                .ToListAsync(cancellationToken);
            var nameLookup = nameByUser.GroupBy(n => n.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

            var deptIds = rows.Where(r => r.PrimaryDepartmentID.HasValue).Select(r => r.PrimaryDepartmentID!.Value).Distinct().ToList();
            var deptNameById = await _context.Departments
                .Where(dept => deptIds.Contains(dept.DepartmentID))
                .ToDictionaryAsync(dept => dept.DepartmentID, dept => dept.Name, cancellationToken);

            var specialityIds = rows.Where(r => r.PrimaryMedicalSpecialityId.HasValue).Select(r => r.PrimaryMedicalSpecialityId!.Value).Distinct().ToList();
            var categoryById = await _context.MedicalSpecialities
                .Where(s => specialityIds.Contains(s.SpecialityId))
                .ToDictionaryAsync(s => s.SpecialityId, s => s.PatientFacingCategory, cancellationToken);

            var doctors = rows
                .Select(r => new PublicDoctorRosterItem
                {
                    DoctorId = r.DoctorID,
                    FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    SpecialtyCategory = r.PrimaryMedicalSpecialityId.HasValue && categoryById.TryGetValue(r.PrimaryMedicalSpecialityId.Value, out var sc) ? sc : null,
                })
                .OrderBy(d => d.FullName)
                .ToList();

            var response = new GetPublicDoctorRosterResponseModel { Success = true, Doctors = doctors };
            _cache.Set(cacheKey, response, CacheTtl);
            return response;
        }
    }
}
