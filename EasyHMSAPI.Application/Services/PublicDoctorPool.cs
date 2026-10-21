using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// One lightweight row per doctor who is live on Doctor Dekho -- just what Vita's search needs
    /// to filter and rank (name, degree text, specialty category, hospital city and coordinates),
    /// no photos, fees or reviews. The full public record is fetched afterwards, for the few
    /// doctors actually returned, through GetPublicDoctorsHandler, so presigned photo URLs and
    /// availability are only resolved for those.
    ///
    /// Eligibility mirrors GetPublicDoctorsHandler exactly: hospital active and not archived, and
    /// either self-listed or holding a CMS force-listed doctor; doctor IsPubliclyListed, not
    /// delisted, user not Revoked; canonical hospital = the lowest eligible HospitalId, the same
    /// deterministic pick that handler makes, so the enriched record names the same hospital.
    /// With ownHospitalId set (Vita inside a hospital's own channel) that hospital's doctors are
    /// added even if the hospital never opted into the marketplace -- the same consent context as
    /// the OPD QR flow -- and that hospital becomes their canonical one.
    /// Cached per ownHospitalId for 60 s, like the public directory.
    /// </summary>
    public static class PublicDoctorPool
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public sealed class Entry
        {
            public Guid DoctorId { get; init; }
            public string FullName { get; init; } = string.Empty;
            public string? Qualification { get; init; }
            public int? ExperienceYears { get; init; }
            public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();
            // MedicalSpecialities.PatientFacingCategory, falling back to the Department name --
            // the same fallback GetPublicDoctors' SpecialtyCategory filter applies.
            public string? Category { get; init; }
            public bool IsFeatured { get; init; }
            public Guid HospitalId { get; init; }
            public string? HospitalCity { get; init; }
            public double? HospitalLatitude { get; init; }
            public double? HospitalLongitude { get; init; }
            // True when this doctor's canonical hospital is the ownHospitalId the pool was built for.
            public bool AtOwnHospital { get; init; }
        }

        public static string CacheKey(Guid? ownHospitalId) => $"mcp:doctor-pool:{ownHospitalId}";

        public static async Task<IReadOnlyList<Entry>> LoadAsync(AppDbContext context, IMemoryCache cache, Guid? ownHospitalId, CancellationToken cancellationToken)
        {
            var cacheKey = CacheKey(ownHospitalId);
            if (cache.TryGetValue(cacheKey, out IReadOnlyList<Entry>? cached) && cached != null)
                return cached;

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var activeHospitalIds = (await context.Hospitals
                .Where(h => h.IsActive && !h.IsArchived)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken)).ToHashSet();

            var selfListedIds = await context.Hospitals
                .Where(h => h.IsActive && !h.IsArchived && h.IsPubliclyListed)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            var forcedListingHospitalIds = await context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue)
                .Join(context.Doctors.Where(d => d.IsPubliclyListed && !d.IsDelistedByAdmin),
                      dd => dd.DoctorID, d => d.DoctorID, (dd, d) => dd.HospitalId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var publicHospitalIds = selfListedIds
                .Union(forcedListingHospitalIds.Where(activeHospitalIds.Contains))
                .ToHashSet();

            var own = ownHospitalId.HasValue && activeHospitalIds.Contains(ownHospitalId.Value) ? ownHospitalId : null;

            var scope = publicHospitalIds.ToList();
            if (own.HasValue && !publicHospitalIds.Contains(own.Value)) scope.Add(own.Value);
            if (scope.Count == 0) return Store(cache, cacheKey, new List<Entry>());

            var pairs = await context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue && scope.Contains(dd.HospitalId!.Value))
                .Select(dd => new { dd.DoctorID, HospitalId = dd.HospitalId!.Value })
                .Distinct()
                .ToListAsync(cancellationToken);

            var canonical = new Dictionary<Guid, (Guid HospitalId, bool AtOwn)>();
            foreach (var group in pairs.GroupBy(p => p.DoctorID))
            {
                if (own.HasValue && group.Any(p => p.HospitalId == own.Value))
                {
                    canonical[group.Key] = (own.Value, true);
                    continue;
                }
                var publicHospitals = group.Where(p => publicHospitalIds.Contains(p.HospitalId)).Select(p => p.HospitalId).OrderBy(id => id).ToList();
                if (publicHospitals.Count > 0) canonical[group.Key] = (publicHospitals[0], false);
            }
            if (canonical.Count == 0) return Store(cache, cacheKey, new List<Entry>());

            var candidateIds = canonical.Keys.ToList();
            var rows = await (
                from d in context.Doctors
                where candidateIds.Contains(d.DoctorID) && d.IsPubliclyListed && !d.IsDelistedByAdmin
                join u in context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                join up in context.UserProfiles on u.UserID equals up.UserID
                select new
                {
                    d.DoctorID, up.FullName, d.Qualification, d.ExperienceYears, d.LanguagesJson,
                    d.PrimaryMedicalSpecialityId, d.PrimaryDepartmentID, d.IsFeatured,
                }).ToListAsync(cancellationToken);

            var specialityIds = rows.Where(r => r.PrimaryMedicalSpecialityId.HasValue).Select(r => r.PrimaryMedicalSpecialityId!.Value).Distinct().ToList();
            var categoryBySpeciality = await context.MedicalSpecialities
                .Where(s => specialityIds.Contains(s.SpecialityId) && s.IsActive)
                .Select(s => new { s.SpecialityId, s.PatientFacingCategory })
                .ToDictionaryAsync(s => s.SpecialityId, s => s.PatientFacingCategory, cancellationToken);

            var departmentIds = rows.Where(r => r.PrimaryDepartmentID.HasValue).Select(r => r.PrimaryDepartmentID!.Value).Distinct().ToList();
            var departmentNameById = await context.Departments
                .Where(dep => departmentIds.Contains(dep.DepartmentID) && dep.IsActive)
                .Select(dep => new { dep.DepartmentID, dep.Name })
                .ToDictionaryAsync(dep => dep.DepartmentID, dep => dep.Name, cancellationToken);

            var hospitalIdsUsed = canonical.Values.Select(v => v.HospitalId).Distinct().ToList();
            var hospitalById = await context.Hospitals
                .Where(h => hospitalIdsUsed.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.City, h.Latitude, h.Longitude })
                .ToDictionaryAsync(h => h.HospitalID, cancellationToken);

            var entries = new List<Entry>(rows.Count);
            foreach (var r in rows)
            {
                var (hospitalId, atOwn) = canonical[r.DoctorID];
                hospitalById.TryGetValue(hospitalId, out var hospital);

                string? category = null;
                if (r.PrimaryMedicalSpecialityId.HasValue && categoryBySpeciality.TryGetValue(r.PrimaryMedicalSpecialityId.Value, out var cat) && !string.IsNullOrWhiteSpace(cat))
                    category = cat;
                else if (r.PrimaryDepartmentID.HasValue && departmentNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dept) && !string.IsNullOrWhiteSpace(dept))
                    category = dept;

                entries.Add(new Entry
                {
                    DoctorId = r.DoctorID,
                    FullName = r.FullName ?? string.Empty,
                    Qualification = r.Qualification,
                    ExperienceYears = r.ExperienceYears,
                    Languages = ParseLanguages(r.LanguagesJson),
                    Category = category,
                    IsFeatured = r.IsFeatured,
                    HospitalId = hospitalId,
                    HospitalCity = hospital?.City,
                    HospitalLatitude = hospital?.Latitude is decimal lat ? (double)lat : null,
                    HospitalLongitude = hospital?.Longitude is decimal lng ? (double)lng : null,
                    AtOwnHospital = atOwn,
                });
            }

            return Store(cache, cacheKey, entries);
        }

        private static IReadOnlyList<Entry> Store(IMemoryCache cache, string key, List<Entry> entries)
        {
            cache.Set(key, (IReadOnlyList<Entry>)entries, CacheTtl);
            return entries;
        }

        private static IReadOnlyList<string> ParseLanguages(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
