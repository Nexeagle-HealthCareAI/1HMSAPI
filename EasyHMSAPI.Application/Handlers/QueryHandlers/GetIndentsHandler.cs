using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetIndentsHandler :
        IRequestHandler<GetIndentsRequestModel, GetIndentsResponseModel>,
        IRequestHandler<GetIndentDetailRequestModel, GetIndentDetailResponseModel>
    {
        private readonly AppDbContext _context;

        public GetIndentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetIndentsResponseModel> Handle(GetIndentsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Indent.Where(i => i.HospitalId == request.HospitalId);
            if (!string.IsNullOrWhiteSpace(request.Status))
                query = query.Where(i => i.Status == request.Status.Trim().ToUpperInvariant());

            var indents = await query.OrderByDescending(i => i.RequestedAt).ToListAsync(cancellationToken);
            var storeIds = indents.Select(i => i.RequestingStoreId)
                .Concat(indents.Where(i => i.TargetStoreId.HasValue).Select(i => i.TargetStoreId!.Value))
                .Distinct().ToList();
            var storeNames = await _context.Store
                .Where(s => storeIds.Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);
            var lineCounts = await _context.IndentLine
                .Where(l => indents.Select(i => i.IndentId).Contains(l.IndentId))
                .GroupBy(l => l.IndentId)
                .Select(g => new { IndentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.IndentId, x => x.Count, cancellationToken);

            var result = indents.Select(i => new IndentDataModel
            {
                IndentId = i.IndentId,
                IndentNumber = i.IndentNumber,
                RequestingStoreId = i.RequestingStoreId,
                RequestingStoreName = storeNames.TryGetValue(i.RequestingStoreId, out var name) ? name : null,
                TargetStoreId = i.TargetStoreId,
                TargetStoreName = i.TargetStoreId.HasValue && storeNames.TryGetValue(i.TargetStoreId.Value, out var targetName) ? targetName : null,
                Status = i.Status,
                IsSystemGenerated = i.IsSystemGenerated,
                RequestedBy = i.RequestedBy,
                RequestedAt = i.RequestedAt,
                LineCount = lineCounts.TryGetValue(i.IndentId, out var count) ? count : 0,
            }).ToList();

            return new GetIndentsResponseModel { Indents = result };
        }

        public async Task<GetIndentDetailResponseModel> Handle(GetIndentDetailRequestModel request, CancellationToken cancellationToken)
        {
            var indent = await _context.Indent.FirstOrDefaultAsync(
                i => i.IndentId == request.IndentId && i.HospitalId == request.HospitalId, cancellationToken);
            if (indent == null)
                return new GetIndentDetailResponseModel { Success = false, Message = "Indent not found." };

            var storeIds = new List<Guid> { indent.RequestingStoreId };
            if (indent.TargetStoreId.HasValue) storeIds.Add(indent.TargetStoreId.Value);

            var storeNames = await _context.Store
                .Where(s => storeIds.Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var lines = await _context.IndentLine.Where(l => l.IndentId == indent.IndentId).ToListAsync(cancellationToken);
            var itemsById = await _context.InventoryItem
                .Where(x => lines.Select(l => l.InventoryItemId).Contains(x.InventoryItemId))
                .ToDictionaryAsync(x => x.InventoryItemId, cancellationToken);

            return new GetIndentDetailResponseModel
            {
                Success = true,
                Indent = new IndentDataModel
                {
                    IndentId = indent.IndentId,
                    IndentNumber = indent.IndentNumber,
                    RequestingStoreId = indent.RequestingStoreId,
                    RequestingStoreName = storeNames.TryGetValue(indent.RequestingStoreId, out var reqName) ? reqName : null,
                    TargetStoreId = indent.TargetStoreId,
                    TargetStoreName = indent.TargetStoreId.HasValue && storeNames.TryGetValue(indent.TargetStoreId.Value, out var tgtName) ? tgtName : null,
                    Status = indent.Status,
                    IsSystemGenerated = indent.IsSystemGenerated,
                    RequestedBy = indent.RequestedBy,
                    RequestedAt = indent.RequestedAt,
                    LineCount = lines.Count,
                },
                Lines = lines.Select(l => new IndentLineDataModel
                {
                    IndentLineId = l.IndentLineId,
                    InventoryItemId = l.InventoryItemId,
                    ItemName = itemsById.TryGetValue(l.InventoryItemId, out var item) ? item.ItemName : "Unknown",
                    Unit = itemsById.TryGetValue(l.InventoryItemId, out var item2) ? item2.Unit : "",
                    Qty = l.Qty,
                    IssuedQty = l.IssuedQty,
                    Notes = l.Notes,
                }).ToList(),
            };
        }
    }
}
