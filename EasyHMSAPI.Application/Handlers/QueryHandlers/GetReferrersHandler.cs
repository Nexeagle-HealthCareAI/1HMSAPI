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

            return new GetReferrersResponseModel { Referrers = referrers };
        }
    }
}
