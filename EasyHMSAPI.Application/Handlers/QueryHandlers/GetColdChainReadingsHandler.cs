using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetColdChainReadingsHandler : IRequestHandler<GetColdChainReadingsRequestModel, GetColdChainReadingsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetColdChainReadingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetColdChainReadingsResponseModel> Handle(GetColdChainReadingsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.ColdChainTempLog.Where(c => c.HospitalId == request.HospitalId);
            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                query = query.Where(c => c.StoreId == request.StoreId);

            var readings = await query.OrderByDescending(c => c.RecordedAt).Take(200).ToListAsync(cancellationToken);
            var storeNames = await _context.Store
                .Where(s => readings.Select(r => r.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var result = readings.Select(r => new ColdChainReadingDataModel
            {
                LogId = r.LogId,
                StoreId = r.StoreId,
                StoreName = storeNames.TryGetValue(r.StoreId, out var name) ? name : null,
                RecordedAt = r.RecordedAt,
                TempCelsius = r.TempCelsius,
                RecordedBy = r.RecordedBy,
                BreachFlag = r.BreachFlag,
            }).ToList();

            return new GetColdChainReadingsResponseModel { Readings = result };
        }
    }
}
