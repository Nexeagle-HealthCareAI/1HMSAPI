using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Hospital lookup behind Vita's `search_hospitals` MCP tool. A hospital qualifies when it has
    /// doctors live on Doctor Dekho (PublicDoctorPool -- the same eligibility search_doctors uses)
    /// or it opted into the public directory itself. Filters: name (HospitalNameMatcher, which also
    /// accepts the city inside the query), city, distance. Vita then calls search_doctors with
    /// hospitalId + onlyAtHospital to list that hospital's doctors.
    /// </summary>
    public class SearchPublicHospitalsHandler : IRequestHandler<SearchPublicHospitalsRequestModel, SearchPublicHospitalsResponseModel>
    {
        public const double DefaultRadiusKm = 25;

        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public SearchPublicHospitalsHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<SearchPublicHospitalsResponseModel> Handle(SearchPublicHospitalsRequestModel request, CancellationToken cancellationToken)
        {
            var limit = Math.Clamp(request.Limit <= 0 ? 5 : request.Limit, 1, SearchPublicDoctorsHandler.MaxLimit);
            var hasPoint = request.Latitude.HasValue && request.Longitude.HasValue;
            var hasQuery = !string.IsNullOrWhiteSpace(request.Query);

            var pool = await PublicDoctorPool.LoadAsync(_context, _cache, null, cancellationToken);
            var doctorCount = pool.GroupBy(e => e.HospitalId).ToDictionary(g => g.Key, g => g.Count());
            var poolIds = doctorCount.Keys.ToList();

            var hospitals = await _context.Hospitals.AsNoTracking()
                .Where(h => h.IsActive && !h.IsArchived && (h.IsPubliclyListed || poolIds.Contains(h.HospitalID)))
                .Select(h => new
                {
                    h.HospitalID, h.Name, h.Location, h.City, h.State, h.Pincode, h.Contact, h.AlternateContact,
                    h.Latitude, h.Longitude,
                })
                .ToListAsync(cancellationToken);

            var matches = new List<PublicHospitalMatch>();
            foreach (var h in hospitals)
            {
                if (!string.IsNullOrWhiteSpace(request.City)
                    && !string.Equals(h.City?.Trim(), request.City.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                double? score = null;
                if (hasQuery)
                {
                    score = HospitalNameMatcher.Score(request.Query, h.Name, h.City);
                    if (score < HospitalNameMatcher.MatchThreshold) continue;
                }

                double? distance = null;
                var lat = h.Latitude is decimal la ? (double)la : (double?)null;
                var lng = h.Longitude is decimal lo ? (double)lo : (double?)null;
                if (hasPoint)
                {
                    if (lat == null || lng == null) { if (!hasQuery) continue; }
                    else
                    {
                        distance = Math.Round(SearchPublicDoctorsHandler.HaversineKm(
                            request.Latitude!.Value, request.Longitude!.Value, lat.Value, lng.Value), 1);
                        var radius = request.MaxDistanceKm is > 0 ? request.MaxDistanceKm.Value : DefaultRadiusKm;
                        // A named hospital is found wherever it is; the radius only limits "hospitals near me".
                        if (!hasQuery && distance > radius) continue;
                    }
                }

                var contact = string.IsNullOrWhiteSpace(h.Contact) ? h.AlternateContact : h.Contact;
                matches.Add(new PublicHospitalMatch
                {
                    HospitalId = h.HospitalID,
                    Name = h.Name,
                    Address = h.Location,
                    City = h.City,
                    State = h.State,
                    Pincode = h.Pincode,
                    Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim(),
                    Latitude = lat,
                    Longitude = lng,
                    DistanceKm = distance,
                    NameMatchScore = score,
                    DoctorCount = doctorCount.TryGetValue(h.HospitalID, out var n) ? n : 0,
                });
            }

            var ranked = matches
                .OrderByDescending(m => m.NameMatchScore ?? 0)
                .ThenBy(m => m.DistanceKm ?? double.MaxValue)
                .ThenByDescending(m => m.DoctorCount)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new SearchPublicHospitalsResponseModel
            {
                Success = true,
                Total = ranked.Count,
                Hospitals = ranked.Take(limit).ToList(),
            };
        }
    }
}
