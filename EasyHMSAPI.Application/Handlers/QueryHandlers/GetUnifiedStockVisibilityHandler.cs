using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetUnifiedStockVisibilityHandler : IRequestHandler<GetUnifiedStockVisibilityRequestModel, GetUnifiedStockVisibilityResponseModel>
    {
        private readonly AppDbContext _context;

        public GetUnifiedStockVisibilityHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetUnifiedStockVisibilityResponseModel> Handle(GetUnifiedStockVisibilityRequestModel request, CancellationToken cancellationToken)
        {
            var stores = await _context.Store
                .Where(s => s.HospitalId == request.HospitalId)
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var stockLevels = await _context.StockLevel
                .Where(sl => sl.HospitalId == request.HospitalId && sl.QtyOnHand > 0)
                .ToListAsync(cancellationToken);
            var inventoryByStore = stockLevels
                .GroupBy(sl => stores.TryGetValue(sl.StoreId, out var name) ? name : "Unknown Store")
                .Select(g => new StoreStockSummaryRow { StoreName = g.Key, ItemCount = g.Select(x => x.InventoryItemId).Distinct().Count() })
                .OrderBy(r => r.StoreName)
                .ToList();

            var bloodBags = await _context.BloodBag
                .Where(b => b.HospitalId == request.HospitalId && b.Status != "DISCARDED")
                .ToListAsync(cancellationToken);
            var bloodByStore = bloodBags
                .GroupBy(b => new
                {
                    StoreName = b.StoreId.HasValue && stores.TryGetValue(b.StoreId.Value, out var name) ? name : (b.StorageLocation ?? "Unassigned"),
                    b.Component,
                    b.BloodGroup,
                    b.Status,
                })
                .Select(g => new BloodStockSummaryRow
                {
                    StoreName = g.Key.StoreName,
                    Component = g.Key.Component,
                    BloodGroup = g.Key.BloodGroup,
                    Status = g.Key.Status,
                    BagCount = g.Count(),
                    TotalVolumeMl = g.Sum(x => x.VolumeMl),
                })
                .OrderBy(r => r.StoreName).ThenBy(r => r.Component).ThenBy(r => r.BloodGroup)
                .ToList();

            var instrumentSets = await _context.InstrumentSet
                .Where(i => i.HospitalId == request.HospitalId && i.IsActive)
                .ToListAsync(cancellationToken);
            var cssdByStore = instrumentSets
                .GroupBy(i => new
                {
                    StoreName = i.StoreId.HasValue && stores.TryGetValue(i.StoreId.Value, out var name) ? name : (i.CurrentLocation ?? "Unassigned"),
                    i.CurrentStatus,
                })
                .Select(g => new CssdStockSummaryRow { StoreName = g.Key.StoreName, CurrentStatus = g.Key.CurrentStatus, SetCount = g.Count() })
                .OrderBy(r => r.StoreName).ThenBy(r => r.CurrentStatus)
                .ToList();

            return new GetUnifiedStockVisibilityResponseModel
            {
                InventoryByStore = inventoryByStore,
                BloodByStore = bloodByStore,
                CssdByStore = cssdByStore,
            };
        }
    }
}
