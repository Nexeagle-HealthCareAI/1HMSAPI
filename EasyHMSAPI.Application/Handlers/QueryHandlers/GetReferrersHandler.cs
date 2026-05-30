using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetReferrersHandler : IRequestHandler<GetReferrersRequestModel, GetReferrersResponseModel>
    {
        private readonly AppDbContext _context;
        public GetReferrersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetReferrersResponseModel> Handle(GetReferrersRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Referrers
                .Where(r => r.HospitalId == request.HospitalId);

            if (request.ActiveOnly)
                query = query.Where(r => r.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLower();
                query = query.Where(r =>
                    r.ReferrerName.ToLower().Contains(term) ||
                    (r.Phone != null && r.Phone.Contains(term)));
            }

            var referrers = await query
                .OrderBy(r => r.ReferrerName)
                .Select(r => new ReferrerInfo
                {
                    ReferrerId = r.ReferrerId,
                    ReferrerName = r.ReferrerName,
                    ReferrerType = r.ReferrerType,
                    Phone = r.Phone,
                    Email = r.Email,
                    Address = r.Address,
                    Pan = r.Pan,
                    DefaultRatePercent = r.DefaultRatePercent,
                    IsActive = r.IsActive
                })
                .ToListAsync(cancellationToken);

            // Distinct patients referred by each referrer (computed in memory to avoid a fragile
            // distinct-count-within-group SQL translation).
            var referrerIds = referrers.Select(r => r.ReferrerId).ToList();
            if (referrerIds.Count > 0)
            {
                var pairs = await _context.Appointments
                    .Where(a => a.ReferredByReferrerId != null
                        && referrerIds.Contains(a.ReferredByReferrerId.Value)
                        && a.PatientId != null)
                    .Select(a => new { RefId = a.ReferredByReferrerId!.Value, a.PatientId })
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var countByReferrer = pairs
                    .GroupBy(p => p.RefId)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var r in referrers)
                    r.ReferredPatientCount = countByReferrer.TryGetValue(r.ReferrerId, out var c) ? c : 0;
            }

            return new GetReferrersResponseModel { Referrers = referrers };
        }
    }
}
