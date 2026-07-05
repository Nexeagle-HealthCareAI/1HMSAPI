using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetInventoryBoardHandler : IRequestHandler<GetInventoryBoardRequestModel, GetInventoryBoardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetInventoryBoardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetInventoryBoardResponseModel> Handle(GetInventoryBoardRequestModel request, CancellationToken cancellationToken)
        {
            var items = await _context.InventoryItem
                .Where(i => i.HospitalId == request.HospitalId && i.IsActive)
                .ToListAsync(cancellationToken);
            var itemsById = items.ToDictionary(i => i.InventoryItemId);

            var stores = await _context.Store
                .Where(s => s.HospitalId == request.HospitalId)
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var stockByStore = await _context.StockLevel
                .Where(sl => sl.HospitalId == request.HospitalId && sl.QtyOnHand > 0)
                .ToListAsync(cancellationToken);

            var stockRows = stockByStore
                .Where(sl => itemsById.ContainsKey(sl.InventoryItemId))
                .Select(sl =>
                {
                    var item = itemsById[sl.InventoryItemId];
                    return new StockOverviewRow
                    {
                        InventoryItemId = item.InventoryItemId,
                        ItemName = item.ItemName,
                        Category = item.Category,
                        Unit = item.Unit,
                        StoreId = sl.StoreId,
                        StoreName = stores.TryGetValue(sl.StoreId, out var storeName) ? storeName : "Unknown Store",
                        QtyOnHand = sl.QtyOnHand,
                    };
                })
                .OrderBy(r => r.StoreName).ThenBy(r => r.ItemName)
                .ToList();

            var today = DateTime.UtcNow.Date;
            var expiryHorizon = today.AddDays(90);
            var nearExpiryBatches = await _context.Batch
                .Where(b => b.HospitalId == request.HospitalId && b.Status == "ACTIVE" && b.RemainingQty > 0
                         && b.ExpiryDate != null && b.ExpiryDate <= expiryHorizon)
                .ToListAsync(cancellationToken);

            var expiryAlerts = nearExpiryBatches
                .Where(b => itemsById.ContainsKey(b.InventoryItemId))
                .Select(b =>
                {
                    var daysToExpiry = (int)(b.ExpiryDate!.Value.Date - today).TotalDays;
                    var tier = daysToExpiry <= 30 ? 30 : daysToExpiry <= 60 ? 60 : 90;
                    return new ExpiryAlertRow
                    {
                        BatchId = b.BatchId,
                        InventoryItemId = b.InventoryItemId,
                        ItemName = itemsById[b.InventoryItemId].ItemName,
                        StoreName = stores.TryGetValue(b.StoreId, out var storeName) ? storeName : "Unknown Store",
                        BatchNumber = b.BatchNumber,
                        ExpiryDate = b.ExpiryDate!.Value,
                        DaysToExpiry = daysToExpiry,
                        RemainingQty = b.RemainingQty,
                        Tier = tier,
                    };
                })
                .OrderBy(r => r.DaysToExpiry)
                .ToList();

            var reorderAlerts = items
                .Where(i => i.CurrentStock <= i.MinStockLevel)
                .Select(i => new ReorderAlertRow
                {
                    InventoryItemId = i.InventoryItemId,
                    ItemName = i.ItemName,
                    Category = i.Category,
                    Unit = i.Unit,
                    CurrentStock = i.CurrentStock,
                    MinStockLevel = i.MinStockLevel,
                    ReorderQty = i.ReorderQty,
                })
                .OrderBy(r => r.ItemName)
                .ToList();

            return new GetInventoryBoardResponseModel
            {
                StockByStore = stockRows,
                ExpiryAlerts = expiryAlerts,
                ReorderAlerts = reorderAlerts,
            };
        }
    }
}
