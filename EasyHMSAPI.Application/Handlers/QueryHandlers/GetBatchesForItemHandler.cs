using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBatchesForItemHandler : IRequestHandler<GetBatchesForItemRequestModel, GetBatchesForItemResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBatchesForItemHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBatchesForItemResponseModel> Handle(GetBatchesForItemRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Batch.Where(b => b.HospitalId == request.HospitalId && b.InventoryItemId == request.InventoryItemId);
            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                query = query.Where(b => b.StoreId == request.StoreId);
            if (request.ActiveOnly)
                query = query.Where(b => b.Status == "ACTIVE");

            var batches = await query.ToListAsync(cancellationToken);
            var storeNames = await _context.Store
                .Where(s => batches.Select(b => b.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var result = batches
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.CreatedAt)
                .Select(b => new BatchDataModel
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
                })
                .ToList();

            return new GetBatchesForItemResponseModel { Batches = result };
        }
    }

    public class GetBatchByBarcodeHandler : IRequestHandler<GetBatchByBarcodeRequestModel, GetBatchByBarcodeResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBatchByBarcodeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBatchByBarcodeResponseModel> Handle(GetBatchByBarcodeRequestModel request, CancellationToken cancellationToken)
        {
            var barcode = request.BarcodeValue?.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
                return new GetBatchByBarcodeResponseModel { Found = false };

            var query = _context.Batch.Where(b => b.HospitalId == request.HospitalId && b.BarcodeValue == barcode && b.Status == "ACTIVE");
            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                query = query.Where(b => b.StoreId == request.StoreId);

            var batch = await query.OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue).FirstOrDefaultAsync(cancellationToken);
            if (batch == null)
                return new GetBatchByBarcodeResponseModel { Found = false };

            var itemName = await _context.InventoryItem
                .Where(i => i.InventoryItemId == batch.InventoryItemId)
                .Select(i => i.ItemName)
                .FirstOrDefaultAsync(cancellationToken);
            var storeName = await _context.Store
                .Where(s => s.StoreId == batch.StoreId)
                .Select(s => s.StoreName)
                .FirstOrDefaultAsync(cancellationToken);

            return new GetBatchByBarcodeResponseModel
            {
                Found = true,
                InventoryItemId = batch.InventoryItemId,
                ItemName = itemName,
                Batch = new BatchDataModel
                {
                    BatchId = batch.BatchId,
                    StoreId = batch.StoreId,
                    StoreName = storeName,
                    BatchNumber = batch.BatchNumber,
                    ManufactureDate = batch.ManufactureDate,
                    ExpiryDate = batch.ExpiryDate,
                    UnitCost = batch.UnitCost,
                    Mrp = batch.Mrp,
                    BarcodeValue = batch.BarcodeValue,
                    ReceivedQty = batch.ReceivedQty,
                    RemainingQty = batch.RemainingQty,
                    Status = batch.Status,
                }
            };
        }
    }
}
