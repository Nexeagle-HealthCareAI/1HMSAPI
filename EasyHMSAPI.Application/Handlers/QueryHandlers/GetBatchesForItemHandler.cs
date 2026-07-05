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
                    ReceivedQty = b.ReceivedQty,
                    RemainingQty = b.RemainingQty,
                    Status = b.Status,
                })
                .ToList();

            return new GetBatchesForItemResponseModel { Batches = result };
        }
    }
}
