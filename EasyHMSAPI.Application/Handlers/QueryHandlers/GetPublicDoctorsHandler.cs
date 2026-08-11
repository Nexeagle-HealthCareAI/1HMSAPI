using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) doctor listing — platform-wide, spans every hospital that has
    /// opted into the public directory (Hospital.IsPubliclyListed), not scoped to one hospital.
    /// A doctor only appears when BOTH their hospital and the doctor themself (Doctor.IsPubliclyListed)
    /// have opted in — hospital-scope resolved via DoctorDepartments, not the single retrofitted
    /// Doctor.HospitalId field (see GetDoctorFeesHandler for the same convention).
    /// Deliberately narrower than GetDepartmentDoctorsHandler / GetHospitalDoctorsHandler:
    /// excludes LicenseNumber, MedicalCouncil, RegistrationYear, UserId, and any mobile/email/
    /// queue-internal field, and additionally resolves a fresh presigned photo URL per doctor
    /// (same GetUrlAsync pattern as GetProfilePictureHandler — presigned URLs expire, so this is
    /// never cached long-term).
    /// </summary>
    public class GetPublicDoctorsHandler : IRequestHandler<GetPublicDoctorsRequestModel, GetPublicDoctorsResponseModel>
    {
        private const string OpdConsultFeeType = "OPD_CONSULT";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobService;
        private readonly IMemoryCache _cache;
        private readonly string _containerName;

        public GetPublicDoctorsHandler(AppDbContext context, IBlobStorageService blobService, IMemoryCache cache, IConfiguration configuration)
        {
            _context = context;
            _blobService = blobService;
            _cache = cache;
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
        }

        public async Task<GetPublicDoctorsResponseModel> Handle(GetPublicDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            // Default (24) serves the paginated patient-facing browse UI. The cap (2000) is much
            // higher than that on purpose: NexEagleWebsite's server.ts also calls this same
            // endpoint for SSR page generation and single-doctor lookup (getAllDoctors/
            // getDoctorById — no dedicated GET /public/doctors/{id} endpoint exists yet), and both
            // explicitly request a large PageSize to fetch the whole directory in one call, exactly
            // matching their pre-pagination behavior. 2000 comfortably covers today's ~1,080
            // doctors with room to grow; revisit if the platform gets meaningfully bigger than that.
            var pageSize = request.PageSize < 1 ? 24 : Math.Min(request.PageSize, 2000);

            // One cache entry per filter combo now (not one whole-platform entry) — see
            // PublicDirectoryCacheKeys.PublicDoctorsList. Presigned photo URLs are valid for 24h
            // (Storage:S3:UrlExpiryHours), so a 60s-stale cache entry never serves an expired one.
            var cacheKey = PublicDirectoryCacheKeys.PublicDoctorsList(page, pageSize, request.City, request.State, request.SpecialtyCategory, request.Search, request.HospitalId, request.DoctorId);
            if (_cache.TryGetValue(cacheKey, out GetPublicDoctorsResponseModel? cached) && cached != null)
            {
                return cached;
            }

            // Every query in this handler is read-only — this is the platform-wide doctor
            // directory, hit repeatedly by many concurrent browsing users. Disabling change
            // tracking for the whole request skips the identity-map/snapshot bookkeeping EF Core
            // otherwise does for every row, which is pure waste on data that's never saved back.
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var hospitalQuery = _context.Hospitals.Where(h => h.IsActive && !h.IsArchived);
            if (request.HospitalId.HasValue)
            {
                // Scanning your own hospital's QR code is a different consent context than
                // platform-wide marketplace browsing -- bypass IsPubliclyListed, but a deactivated/
                // archived hospital still returns nothing either way.
                hospitalQuery = hospitalQuery.Where(h => h.HospitalID == request.HospitalId.Value);
            }
            else
            {
                hospitalQuery = hospitalQuery.Where(h => h.IsPubliclyListed);
            }
            if (!string.IsNullOrWhiteSpace(request.City))
                hospitalQuery = hospitalQuery.Where(h => h.City == request.City);
            if (!string.IsNullOrWhiteSpace(request.State))
                hospitalQuery = hospitalQuery.Where(h => h.State == request.State);

            var publicHospitalIds = await hospitalQuery.Select(h => h.HospitalID).ToListAsync(cancellationToken);

            if (publicHospitalIds.Count == 0)
                return EmptyResult(page, pageSize);

            // Doctor -> hospital affiliations restricted to publicly-listed hospitals (now also
            // narrowed by City/State above). Doctor.HospitalId is a single retrofitted field, not
            // the source of truth (see GetDoctorFeesHandler) — a doctor is a global identity that
            // can have DoctorDepartments rows at more than one hospital. True multi-hospital doctor
            // practice isn't a live product scenario yet, so pick one hospital deterministically
            // per doctor rather than fanning out to multiple rows.
            var doctorHospitalPairs = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue && publicHospitalIds.Contains(dd.HospitalId!.Value))
                .Select(dd => new { dd.DoctorID, HospitalId = dd.HospitalId!.Value })
                .Distinct()
                .ToListAsync(cancellationToken);

            var doctorHospital = doctorHospitalPairs
                .GroupBy(p => p.DoctorID)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.HospitalId).First().HospitalId);

            if (doctorHospital.Count == 0)
                return EmptyResult(page, pageSize);

            var candidateDoctorIds = doctorHospital.Keys.ToList();

            // City/State/Specialty/Search/sort all happen in SQL, and Skip/Take runs BEFORE any of
            // the expensive per-doctor enrichment below (hospital/department/speciality lookups,
            // review aggregates, fee lookups, and — the important one at scale — presigned photo
            // URL resolution, which is a real S3 network call per doctor). Joining UserProfiles here
            // (rather than in a separate batch-by-id lookup, like the old code did) is what lets
            // Search match on name and lets FullName drive the ORDER BY before paging.
            var filteredQuery =
                from d in _context.Doctors
                where candidateDoctorIds.Contains(d.DoctorID) && d.IsPubliclyListed && !d.IsDelistedByAdmin
                join u in _context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                join up in _context.UserProfiles on u.UserID equals up.UserID
                select new { d, up.FullName };

            if (request.DoctorId.HasValue)
            {
                filteredQuery = filteredQuery.Where(x => x.d.DoctorID == request.DoctorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SpecialtyCategory))
            {
                filteredQuery =
                    from x in filteredQuery
                    join ms in _context.MedicalSpecialities on x.d.PrimaryMedicalSpecialityId equals ms.SpecialityId
                    where ms.PatientFacingCategory == request.SpecialtyCategory
                    select x;
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                filteredQuery = filteredQuery.Where(x => x.FullName.Contains(term) || (x.d.Qualification != null && x.d.Qualification.Contains(term)));
            }

            var totalCount = await filteredQuery.CountAsync(cancellationToken);

            // Featured doctors first, alphabetical within each group — matches the sort NexEagle's
            // own DoctorDirectory.tsx used to apply client-side; now done in SQL, before paging.
            var pageRows = await filteredQuery
                .OrderByDescending(x => x.d.IsFeatured)
                .ThenBy(x => x.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.d.DoctorID, x.d.UserID, x.d.PrimaryDepartmentID, x.d.PrimaryMedicalSpecialityId, x.d.Qualification, x.d.ExperienceYears, x.d.Bio, x.d.LanguagesJson,
                    x.d.IsFeatured, x.d.DiscountPercent, x.d.DiscountStartAt, x.d.DiscountEndAt, x.d.IsRegistrationVerified, x.d.IsOnlineNow,
                    x.FullName
                })
                .ToListAsync(cancellationToken);

            if (pageRows.Count == 0)
                return EmptyResult(page, pageSize, totalCount);

            var pageDoctorIds = pageRows.Select(r => r.DoctorID).ToList();

            var hospitalIdsUsed = pageRows.Select(r => doctorHospital[r.DoctorID]).Distinct().ToList();
            var hospitalById = await _context.Hospitals
                .Where(h => hospitalIdsUsed.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.Name, h.Location, h.City, h.State, h.Pincode, h.Latitude, h.Longitude })
                .ToDictionaryAsync(h => h.HospitalID, cancellationToken);

            var deptIds = pageRows.Where(r => r.PrimaryDepartmentID.HasValue).Select(r => r.PrimaryDepartmentID!.Value).Distinct().ToList();
            var deptNameById = await _context.Departments
                .Where(dept => deptIds.Contains(dept.DepartmentID))
                .ToDictionaryAsync(dept => dept.DepartmentID, dept => dept.Name, cancellationToken);

            var specialityIds = pageRows.Where(r => r.PrimaryMedicalSpecialityId.HasValue).Select(r => r.PrimaryMedicalSpecialityId!.Value).Distinct().ToList();
            var specialityById = await _context.MedicalSpecialities
                .Where(s => specialityIds.Contains(s.SpecialityId))
                .Select(s => new { s.SpecialityId, s.PatientFacingName, s.PatientFacingCategory })
                .ToDictionaryAsync(s => s.SpecialityId, cancellationToken);

            // Scoped to this page's doctors only now, not the whole candidate set.
            var reviewAggregates = await _context.DoctorReviews
                .Where(r => pageDoctorIds.Contains(r.DoctorId) && !r.IsHidden && !r.IsHospitalResponse)
                .GroupBy(r => r.DoctorId)
                .Select(g => new { DoctorId = g.Key, Average = g.Average(r => r.Rating), Count = g.Count() })
                .ToDictionaryAsync(g => g.DoctorId, cancellationToken);

            // Keyed by (DoctorId, HospitalId) — not DoctorId alone — since a doctor could have a
            // DoctorFees row at more than one hospital; must match the SAME canonical hospital
            // doctorHospital picked for this listing, not just any fee row for the doctor.
            var feeLookup = (await _context.DoctorFees
                .Where(f => f.FeeType == OpdConsultFeeType && f.IsActive
                         && pageDoctorIds.Contains(f.DoctorId) && hospitalIdsUsed.Contains(f.HospitalId))
                .Select(f => new { f.DoctorId, f.HospitalId, f.Amount })
                .ToListAsync(cancellationToken))
                .ToDictionary(f => (f.DoctorId, f.HospitalId), f => f.Amount);

            // "Available today" — same TimeOff > Override > Template precedence as the single-doctor
            // GetPublicDoctorAvailabilityHandler (DoctorAvailabilityResolver), batched for this
            // page's doctors so the directory grid never needs a per-card call. TimeOffs/Overrides
            // are only counted when their HospitalId matches the SAME canonical hospital picked for
            // this doctor above — mirrors the (DoctorId, HospitalId) keying feeLookup uses.
            // Storage convention is local/IST calendar dates (no explicit timezone), same as every
            // other doctor-availability entry point in this codebase — so "today" is computed in
            // IST here too, not UTC (IST = UTC+5:30).
            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;

            var timeOffRows = await _context.DoctorTimeOffs
                .Where(to => pageDoctorIds.Contains(to.DoctorID) && hospitalIdsUsed.Contains(to.HospitalId)
                          && todayIst >= to.FromDate.Date && todayIst <= to.ToDate.Date)
                .ToListAsync(cancellationToken);
            var timeOffsByDoctor = timeOffRows
                .Where(to => doctorHospital.TryGetValue(to.DoctorID, out var canonicalHospitalId) && canonicalHospitalId == to.HospitalId)
                .GroupBy(to => to.DoctorID)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorTimeOff>)g.ToList());

            var overrideRows = await _context.DoctorShiftOverrides
                .Where(o => pageDoctorIds.Contains(o.DoctorID) && hospitalIdsUsed.Contains(o.HospitalId)
                         && o.StartDate <= todayIst && (!o.EndDate.HasValue || o.EndDate >= todayIst))
                .ToListAsync(cancellationToken);
            var overridesByDoctor = overrideRows
                .Where(o => doctorHospital.TryGetValue(o.DoctorID, out var canonicalHospitalId) && canonicalHospitalId == o.HospitalId)
                .GroupBy(o => o.DoctorID)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorShiftOverride>)g.ToList());

            var activeTemplates = await _context.DoctorShiftTemplates.Where(t => t.IsActive).ToListAsync(cancellationToken);

            // Photo-URL resolution now fans out to just this page's doctors (~PageSize) instead of
            // every publicly-listed doctor platform-wide — each call is a real S3 ListObjectsV2
            // round trip (S3StorageService.GetUrlAsync), so this is the change that actually caps
            // the concurrent-S3-call spike pagination was introduced to fix.
            var photoUrls = await Task.WhenAll(
                pageRows.Select(r => _blobService.GetUrlAsync(r.UserID, _containerName, cancellationToken))
            );

            var specializationRows = await _context.DoctorSpecializations
                .Where(ds => pageDoctorIds.Contains(ds.DoctorID) && ds.Specialization != null && ds.Specialization.IsActive)
                .Select(ds => new { ds.DoctorID, Name = ds.Specialization.Name })
                .ToListAsync(cancellationToken);
            var specializationsByDoctor = specializationRows
                .GroupBy(s => s.DoctorID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => s.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name) &&
                                       name.Trim().Length > 2 &&
                                       name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c)))
                        .Select(name => name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList());

            var nowUtc = DateTime.UtcNow;
            var doctors = new List<PublicDoctorInfo>();
            for (var i = 0; i < pageRows.Count; i++)
            {
                var r = pageRows[i];
                var hospitalId = doctorHospital[r.DoctorID];
                hospitalById.TryGetValue(hospitalId, out var hospital);
                reviewAggregates.TryGetValue(r.DoctorID, out var reviewAgg);
                specialityById.TryGetValue(r.PrimaryMedicalSpecialityId ?? Guid.Empty, out var speciality);
                var specializations = specializationsByDoctor.TryGetValue(r.DoctorID, out var specs) ? specs : new List<string>();
                var fee = feeLookup.TryGetValue((r.DoctorID, hospitalId), out var feeAmount) ? feeAmount : (decimal?)null;
                var discountActive = DoctorMarketingHelpers.IsDiscountActive(r.DiscountPercent, r.DiscountStartAt, r.DiscountEndAt, nowUtc);
                var isAvailableToday = DoctorAvailabilityResolver.IsAvailable(
                    todayIst,
                    timeOffsByDoctor.TryGetValue(r.DoctorID, out var doctorTimeOffs) ? doctorTimeOffs : Array.Empty<DoctorTimeOff>(),
                    overridesByDoctor.TryGetValue(r.DoctorID, out var doctorOverrides) ? doctorOverrides : Array.Empty<DoctorShiftOverride>(),
                    activeTemplates);

                doctors.Add(new PublicDoctorInfo
                {
                    DoctorId = r.DoctorID,
                    FullName = r.FullName,
                    PhotoUrl = photoUrls[i] as string,
                    Qualification = r.Qualification,
                    ExperienceYears = r.ExperienceYears,
                    Bio = r.Bio,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    PrimaryMedicalSpecialityPatientFacingName = speciality?.PatientFacingName,
                    PrimaryMedicalSpecialityCategory = speciality?.PatientFacingCategory,
                    Specializations = specializations,
                    Languages = string.IsNullOrWhiteSpace(r.LanguagesJson)
                        ? new List<string>()
                        : (JsonSerializer.Deserialize<List<string>>(r.LanguagesJson) ?? new List<string>()),
                    HospitalId = hospitalId,
                    HospitalName = hospital?.Name,
                    Address = hospital?.Location,
                    City = hospital?.City,
                    State = hospital?.State,
                    Pincode = hospital?.Pincode,
                    Latitude = hospital?.Latitude,
                    Longitude = hospital?.Longitude,
                    Rating = reviewAgg != null ? Math.Round(reviewAgg.Average, 1) : (double?)null,
                    ReviewCount = reviewAgg?.Count ?? 0,
                    Fee = fee,
                    DiscountPercent = discountActive ? r.DiscountPercent : null,
                    DiscountedFee = discountActive && fee.HasValue
                        ? fee.Value - DoctorMarketingHelpers.ComputeDiscountAmount(fee.Value, r.DiscountPercent!.Value)
                        : null,
                    IsFeatured = r.IsFeatured,
                    IsRegistrationVerified = r.IsRegistrationVerified,
                    IsAvailableToday = isAvailableToday,
                    IsOnlineNow = r.IsOnlineNow,
                });
            }

            var response = new GetPublicDoctorsResponseModel
            {
                Success = true,
                Doctors = doctors, // already ordered by the SQL query above (Featured desc, FullName asc)
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
            _cache.Set(cacheKey, response, CacheTtl);
            return response;
        }

        private static GetPublicDoctorsResponseModel EmptyResult(int page, int pageSize, int totalCount = 0) =>
            new() { Success = true, Doctors = new(), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }
}
