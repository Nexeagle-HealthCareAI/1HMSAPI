using System.Text.Json;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Platform-wide pathology-lab directory for Doctor Dekho -- an INDEPENDENT opt-in
    // (LabConfiguration.IsPubliclyListed alone), unlike the Doctor listing which also requires
    // Hospital.IsPubliclyListed: a lab's visibility doesn't depend on its hospital being separately
    // listed for doctor consultations. LabName/LabAddress/LabRegistrationNumber override the
    // hospital's own generic fields when set, falling back otherwise -- the same resolution
    // resolvePathologyBranding.ts already applies client-side for the printed report letterhead,
    // now needed server-side for the public API response too.
    public class GetPublicLabsHandler : IRequestHandler<GetPublicLabsRequestModel, GetPublicLabsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        // Same TTL-only cache discipline as GetPublicDoctorsHandler -- no explicit invalidation on
        // write (the admin save path doesn't touch IMemoryCache either), a stale listing self-heals
        // within 60s rather than needing new invalidation plumbing this codebase doesn't have for
        // any other public-directory entity.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public GetPublicLabsHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<GetPublicLabsResponseModel> Handle(GetPublicLabsRequestModel request, CancellationToken cancellationToken)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 24 : Math.Min(request.PageSize, 2000);

            var cacheKey = PublicDirectoryCacheKeys.PublicLabsList(page, pageSize, request.City, request.State, request.Search, request.LabId);
            if (_cache.TryGetValue(cacheKey, out GetPublicLabsResponseModel? cached) && cached != null)
            {
                return cached;
            }

            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var query = _context.LabConfiguration
                .Where(l => l.IsPubliclyListed)
                .Join(_context.Hospitals, l => l.HospitalId, h => h.HospitalID, (l, h) => new { l, h })
                .Where(x => x.h.IsActive && !x.h.IsArchived);

            if (request.LabId.HasValue)
            {
                query = query.Where(x => x.l.ConfigId == request.LabId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.City))
            {
                var city = request.City.Trim();
                query = query.Where(x => (x.l.LabCity ?? x.h.City) == city);
            }
            if (!string.IsNullOrWhiteSpace(request.State))
            {
                var state = request.State.Trim();
                query = query.Where(x => (x.l.LabState ?? x.h.State) == state);
            }
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                query = query.Where(x => (x.l.LabName ?? x.h.Name).Contains(term));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var rows = await query
                .OrderBy(x => x.l.LabName ?? x.h.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.l.ConfigId,
                    x.l.HospitalId,
                    Name = x.l.LabName ?? x.h.Name,
                    x.l.PublicDescription,
                    Address = x.l.LabAddress,
                    x.h.Location,
                    HospitalCity = x.h.City,
                    HospitalState = x.h.State,
                    HospitalPincode = x.h.Pincode,
                    x.l.LabCity,
                    x.l.LabState,
                    x.l.LabPincode,
                    x.l.Latitude,
                    x.l.Longitude,
                    RegistrationNumber = x.l.LabRegistrationNumber ?? x.h.RegistrationNumber,
                    x.l.PublicContactPhone,
                    x.l.PublicContactEmail,
                    x.l.TestCategoriesJson,
                })
                .ToListAsync(cancellationToken);

            var labs = rows.Select(r =>
            {
                var categories = new List<string>();
                if (!string.IsNullOrWhiteSpace(r.TestCategoriesJson))
                {
                    try { categories = JsonSerializer.Deserialize<List<string>>(r.TestCategoriesJson) ?? new(); }
                    catch { /* malformed JSON -- treat as no categories rather than fail the whole request */ }
                }
                var address = !string.IsNullOrWhiteSpace(r.Address)
                    ? r.Address
                    : string.Join(", ", new[] { r.Location, r.HospitalCity, r.HospitalState, r.HospitalPincode }.Where(s => !string.IsNullOrWhiteSpace(s)));

                return new PublicLabInfo
                {
                    LabId = r.ConfigId,
                    HospitalId = r.HospitalId,
                    Name = r.Name,
                    Description = r.PublicDescription,
                    Address = address,
                    City = r.LabCity ?? r.HospitalCity,
                    State = r.LabState ?? r.HospitalState,
                    Pincode = r.LabPincode ?? r.HospitalPincode,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    RegistrationNumber = r.RegistrationNumber,
                    ContactPhone = r.PublicContactPhone,
                    ContactEmail = r.PublicContactEmail,
                    TestCategories = categories,
                };
            }).ToList();

            var response = new GetPublicLabsResponseModel
            {
                Success = true,
                Labs = labs,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };

            _cache.Set(cacheKey, response, CacheTtl);
            return response;
        }
    }
}
