using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Doctor search behind Vita's `search_doctors` MCP tool: name (fuzzy), specialty category,
    /// degree, distance, city, language, and "own hospital first". Filtering and ranking run over
    /// PublicDoctorPool (one light row per live doctor, cached 60 s); only the top `Limit` doctors
    /// are then enriched into full PublicDoctorInfo records through GetPublicDoctorsHandler, so the
    /// response carries exactly what Doctor Dekho's own directory shows for them.
    ///
    /// Ranking (relevance): own hospital first when HospitalId is sent; then name-match score when
    /// a name was asked; then distance when a location was sent; then experience; then
    /// available-today; then featured and name as stable tie-breaks. Without a location this gives
    /// the agreed order hospital -> experience -> earliest availability.
    ///
    /// Further filters: OnlyAtHospital (with HospitalId: that hospital's doctors only), Gender,
    /// AvailableToday (working today, IST). Offset skips ranked doctors for "Show more doctors";
    /// Total always counts every match, so the caller knows whether more remain.
    /// </summary>
    public class SearchPublicDoctorsHandler : IRequestHandler<SearchPublicDoctorsRequestModel, SearchPublicDoctorsResponseModel>
    {
        public const double DefaultRadiusKm = 5;
        public const int MaxLimit = 10;
        public const int MaxOffset = 90;

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IMediator _mediator;

        public SearchPublicDoctorsHandler(AppDbContext context, IMemoryCache cache, IMediator mediator)
        {
            _context = context;
            _cache = cache;
            _mediator = mediator;
        }

        private sealed class Candidate
        {
            public PublicDoctorPool.Entry Entry { get; init; } = null!;
            public double? DistanceKm { get; set; }
            public double? NameScore { get; set; }
            public PublicDoctorInfo? Info { get; set; }
        }

        public async Task<SearchPublicDoctorsResponseModel> Handle(SearchPublicDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            var limit = Math.Clamp(request.Limit <= 0 ? 5 : request.Limit, 1, MaxLimit);
            var hasPoint = request.Latitude.HasValue && request.Longitude.HasValue;
            var hasQuery = !string.IsNullOrWhiteSpace(request.Query);

            var pool = await PublicDoctorPool.LoadAsync(_context, _cache, request.HospitalId, cancellationToken);
            IEnumerable<PublicDoctorPool.Entry> filtered = pool;

            var categories = request.SpecialtyCategories?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (categories is { Count: > 0 })
                filtered = filtered.Where(e => e.Category != null && categories.Contains(e.Category.Trim()));

            if (!string.IsNullOrWhiteSpace(request.Degree))
            {
                var degree = NormalizeDegree(request.Degree);
                if (degree.Length > 0)
                    filtered = filtered.Where(e => DegreeTokens(e.Qualification).Contains(degree));
            }

            if (!string.IsNullOrWhiteSpace(request.City))
            {
                var city = request.City.Trim();
                filtered = filtered.Where(e => string.Equals(e.HospitalCity?.Trim(), city, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                var language = request.Language.Trim();
                filtered = filtered.Where(e => e.Languages.Any(l => string.Equals(l?.Trim(), language, StringComparison.OrdinalIgnoreCase)));
            }

            if (request.OnlyAtHospital && request.HospitalId.HasValue)
                filtered = filtered.Where(e => e.AtOwnHospital);

            if (!string.IsNullOrWhiteSpace(request.Gender))
            {
                var wanted = PublicDoctorPool.GenderKey(request.Gender);
                filtered = wanted == null
                    ? Enumerable.Empty<PublicDoctorPool.Entry>()          // an unknown value matches nobody, never everybody
                    : filtered.Where(e => PublicDoctorPool.GenderKey(e.Gender) == wanted);
            }

            var candidates = filtered.Select(e => new Candidate { Entry = e }).ToList();

            if (request.AvailableToday && candidates.Count > 0)
            {
                var working = await WorkingTodayAsync(candidates.Select(c => c.Entry).ToList(), cancellationToken);
                candidates = candidates.Where(c => working.Contains(c.Entry.DoctorId)).ToList();
            }

            if (hasPoint)
            {
                var radius = request.MaxDistanceKm is > 0 ? request.MaxDistanceKm.Value : DefaultRadiusKm;
                foreach (var c in candidates)
                {
                    if (c.Entry.HospitalLatitude.HasValue && c.Entry.HospitalLongitude.HasValue)
                        c.DistanceKm = Math.Round(HaversineKm(request.Latitude!.Value, request.Longitude!.Value,
                            c.Entry.HospitalLatitude.Value, c.Entry.HospitalLongitude.Value), 1);
                }
                // A hospital with no GPS pin cannot be placed inside the radius, so it drops out of
                // a distance search -- except the caller's own hospital, which is always kept.
                candidates = candidates
                    .Where(c => (c.DistanceKm.HasValue && c.DistanceKm.Value <= radius) || (request.HospitalId.HasValue && c.Entry.AtOwnHospital))
                    .ToList();
            }

            if (hasQuery)
            {
                foreach (var c in candidates)
                    c.NameScore = DoctorNameMatcher.Score(request.Query, c.Entry.FullName);
                candidates = candidates.Where(c => c.NameScore >= DoctorNameMatcher.MatchThreshold).ToList();
            }

            var total = candidates.Count;
            if (total == 0)
                return new SearchPublicDoctorsResponseModel { Success = true, Total = 0 };

            var sort = (request.Sort ?? "relevance").Trim().ToLowerInvariant();
            var offset = Math.Clamp(request.Offset, 0, MaxOffset);
            var top = Order(candidates, request, sort, hasQuery, hasPoint, withAvailability: false).Skip(offset).Take(limit).ToList();
            if (top.Count == 0)
                return new SearchPublicDoctorsResponseModel { Success = true, Total = total };

            await EnrichAsync(top, request.HospitalId, cancellationToken);
            top = top.Where(c => c.Info != null).ToList();

            // Re-apply the same order now that today's availability is known for these doctors;
            // OrderBy is stable, so availability only breaks ties left by the earlier keys.
            var ranked = Order(top, request, sort, hasQuery, hasPoint, withAvailability: true).ToList();

            return new SearchPublicDoctorsResponseModel
            {
                Success = true,
                Total = total,
                Doctors = ranked.Select(c => new PublicDoctorMatch
                {
                    Doctor = c.Info!,
                    DistanceKm = c.DistanceKm,
                    SameHospital = request.HospitalId.HasValue ? c.Entry.AtOwnHospital : null,
                    NameMatchScore = c.NameScore,
                }).ToList(),
            };
        }

        private static IEnumerable<Candidate> Order(IEnumerable<Candidate> items, SearchPublicDoctorsRequestModel request,
            string sort, bool hasQuery, bool hasPoint, bool withAvailability)
        {
            var ordered = items.OrderByDescending(c => request.HospitalId.HasValue && c.Entry.AtOwnHospital);

            if (sort == "distance" && hasPoint)
                ordered = ordered.ThenBy(c => c.DistanceKm ?? double.MaxValue);
            else if (sort == "experience")
                ordered = ordered.ThenByDescending(c => c.Entry.ExperienceYears ?? -1);
            else
            {
                if (hasQuery) ordered = ordered.ThenByDescending(c => c.NameScore ?? 0);
                if (hasPoint) ordered = ordered.ThenBy(c => c.DistanceKm ?? double.MaxValue);
            }

            ordered = ordered.ThenByDescending(c => c.Entry.ExperienceYears ?? -1);
            if (withAvailability)
                ordered = ordered.ThenByDescending(c => c.Info?.IsAvailableToday == true);

            return ordered
                .ThenByDescending(c => c.Entry.IsFeatured)
                .ThenBy(c => c.Entry.FullName, StringComparer.OrdinalIgnoreCase);
        }

        // Doctors working today (IST) -- the TimeOff > Override > Template precedence of
        // DoctorAvailabilityResolver, batched, with each doctor's leave and overrides counted only at
        // the canonical hospital the pool picked (as GetPublicDoctorsHandler does for its badge).
        private async Task<HashSet<Guid>> WorkingTodayAsync(List<PublicDoctorPool.Entry> entries, CancellationToken cancellationToken)
        {
            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;
            var ids = entries.Select(e => e.DoctorId).Distinct().ToList();
            var hospitalOf = entries.GroupBy(e => e.DoctorId).ToDictionary(g => g.Key, g => g.First().HospitalId);

            var timeOffs = await _context.DoctorTimeOffs.AsNoTracking()
                .Where(to => ids.Contains(to.DoctorID) && todayIst >= to.FromDate.Date && todayIst <= to.ToDate.Date)
                .ToListAsync(cancellationToken);
            var overrides = await _context.DoctorShiftOverrides.AsNoTracking()
                .Where(o => ids.Contains(o.DoctorID) && o.StartDate <= todayIst && (!o.EndDate.HasValue || o.EndDate >= todayIst))
                .ToListAsync(cancellationToken);
            var templates = await _context.DoctorShiftTemplates.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);

            var offByDoctor = timeOffs.Where(t => hospitalOf[t.DoctorID] == t.HospitalId)
                .GroupBy(t => t.DoctorID).ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorTimeOff>)g.ToList());
            var overridesByDoctor = overrides.Where(o => hospitalOf[o.DoctorID] == o.HospitalId)
                .GroupBy(o => o.DoctorID).ToDictionary(g => g.Key, g => (IReadOnlyCollection<DoctorShiftOverride>)g.ToList());

            return ids.Where(id => DoctorAvailabilityResolver.IsAvailable(
                    todayIst,
                    offByDoctor.TryGetValue(id, out var off) ? off : Array.Empty<DoctorTimeOff>(),
                    overridesByDoctor.TryGetValue(id, out var ov) ? ov : Array.Empty<DoctorShiftOverride>(),
                    templates))
                .ToHashSet();
        }

        // Full public records for the chosen doctors, in at most two calls: the caller's own
        // hospital's doctors through the hospital-scoped path (which also covers a hospital not
        // listed on the marketplace), everyone else through the platform-wide path.
        private async Task EnrichAsync(List<Candidate> chosen, Guid? ownHospitalId, CancellationToken cancellationToken)
        {
            var groups = chosen.GroupBy(c => ownHospitalId.HasValue && c.Entry.AtOwnHospital);
            foreach (var group in groups)
            {
                var ids = group.Select(c => c.Entry.DoctorId).ToList();
                var response = await _mediator.Send(new GetPublicDoctorsRequestModel
                {
                    Page = 1,
                    PageSize = ids.Count,
                    DoctorIds = ids,
                    HospitalId = group.Key ? ownHospitalId : null,
                }, cancellationToken);

                var byId = response.Doctors.ToDictionary(d => d.DoctorId);
                foreach (var c in group)
                    c.Info = byId.TryGetValue(c.Entry.DoctorId, out var info) ? info : null;
            }
        }

        // "M.D." / "md" / "MD" -> "MD"
        public static string NormalizeDegree(string degree) =>
            new string(degree.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        // "MBBS, M.D. (Medicine)" -> { MBBS, MD, MEDICINE }: whole tokens, so "MD" never matches "MDS".
        public static HashSet<string> DegreeTokens(string? qualification)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(qualification)) return tokens;
            var current = new System.Text.StringBuilder();
            foreach (var ch in qualification.ToUpperInvariant())
            {
                if (ch == '.') continue;
                if (char.IsLetterOrDigit(ch)) { current.Append(ch); continue; }
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadiusKm = 6371.0;
            static double Rad(double deg) => deg * Math.PI / 180.0;
            var dLat = Rad(lat2 - lat1);
            var dLng = Rad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
