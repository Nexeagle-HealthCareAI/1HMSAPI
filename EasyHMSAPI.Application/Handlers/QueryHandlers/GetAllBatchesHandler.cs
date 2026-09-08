using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Backs the pharmacy "Stock / Batches" tab — the flat, hospital-wide view of everything currently
    // in stock, so a pharmacist can browse and cross-check what's already there (unlike
    // GetBatchesForItemHandler, scoped to one item, or the near-expiry report, scoped to a 90-day
    // expiry window).
    public class GetAllBatchesHandler : IRequestHandler<GetAllBatchesRequestModel, GetAllBatchesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAllBatchesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAllBatchesResponseModel> Handle(GetAllBatchesRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Batch
                .Where(b => b.HospitalId == request.HospitalId);

            if (request.ActiveOnly)
                query = query.Where(b => b.Status == "ACTIVE");
            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                query = query.Where(b => b.StoreId == request.StoreId);

            var batches = await query.ToListAsync(cancellationToken);

            var itemIds = batches.Select(b => b.InventoryItemId).Distinct().ToList();
            var items = await _context.InventoryItem
                .Where(i => itemIds.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, cancellationToken);
            var storeNames = await _context.Store
                .Where(s => batches.Select(b => b.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var rows = batches
                .Where(b => items.ContainsKey(b.InventoryItemId))
                .Select(b =>
                {
                    var item = items[b.InventoryItemId];
                    return new BatchDataModel
                    {
                        BatchId = b.BatchId,
                        StoreId = b.StoreId,
                        StoreName = storeNames.TryGetValue(b.StoreId, out var name) ? name : null,
                        BatchNumber = b.BatchNumber,
                        ManufactureDate = b.ManufactureDate,
                        ExpiryDate = b.ExpiryDate,
                        UnitCost = b.UnitCost,
                        Mrp = b.Mrp,
                        BarcodeValue = b.BarcodeValue,
                        ReceivedQty = b.ReceivedQty,
                        RemainingQty = b.RemainingQty,
                        Status = b.Status,
                        InventoryItemId = b.InventoryItemId,
                        ItemName = item.ItemName,
                        ItemCode = item.ItemCode,
                    };
                });

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLowerInvariant();
                rows = rows.Where(r =>
                    (r.ItemName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (r.ItemCode?.ToLowerInvariant().Contains(search) ?? false) ||
                    r.BatchNumber.ToLowerInvariant().Contains(search));
            }

            return new GetAllBatchesResponseModel
            {
                Batches = rows.OrderBy(r => r.ItemName).ThenBy(r => r.ExpiryDate ?? DateTime.MaxValue).ToList(),
            };
        }
    }
}
