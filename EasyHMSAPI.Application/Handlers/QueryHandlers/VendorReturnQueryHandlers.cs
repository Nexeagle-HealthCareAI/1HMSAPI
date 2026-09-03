using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class VendorReturnQueryHandlers :
        IRequestHandler<GetRtvEligibleBatchesRequestModel, GetRtvEligibleBatchesResponseModel>,
        IRequestHandler<GetVendorReturnsRequestModel, GetVendorReturnsResponseModel>
    {
        private readonly AppDbContext _context;

        public VendorReturnQueryHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRtvEligibleBatchesResponseModel> Handle(GetRtvEligibleBatchesRequestModel request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;
            var windowEnd = today.AddDays(request.DaysWindow > 0 ? request.DaysWindow : 60);

            var batches = await _context.Batch.AsNoTracking()
                .Where(b => b.HospitalId == request.HospitalId && b.VendorId == request.VendorId
                         && b.Status == "ACTIVE" && b.RemainingQty > 0
                         && b.ExpiryDate != null && b.ExpiryDate.Value.Date <= windowEnd)
                .ToListAsync(cancellationToken);

            var itemNames = await _context.InventoryItem.AsNoTracking()
                .Where(i => batches.Select(b => b.InventoryItemId).Distinct().Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, i => i.ItemName, cancellationToken);

            var rows = batches.Select(b => new RtvEligibleBatchRow
            {
                BatchId = b.BatchId,
                InventoryItemId = b.InventoryItemId,
                ItemName = itemNames.TryGetValue(b.InventoryItemId, out var n) ? n : "Unknown",
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                DaysToExpiry = b.ExpiryDate.HasValue ? (int)(b.ExpiryDate.Value.Date - today).TotalDays : null,
                RemainingQty = b.RemainingQty,
                UnitCost = b.UnitCost,
                EstimatedValue = (b.UnitCost ?? 0) * b.RemainingQty,
            })
            .OrderBy(r => r.DaysToExpiry)
            .ToList();

            return new GetRtvEligibleBatchesResponseModel { Batches = rows };
        }

        public async Task<GetVendorReturnsResponseModel> Handle(GetVendorReturnsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.VendorReturnNote.AsNoTracking().Where(n => n.HospitalId == request.HospitalId);
            if (request.VendorId.HasValue && request.VendorId != Guid.Empty)
                query = query.Where(n => n.VendorId == request.VendorId);

            var notes = await query.OrderByDescending(n => n.GeneratedAt).Take(100).ToListAsync(cancellationToken);
            var noteIds = notes.Select(n => n.VendorReturnId).ToList();

            var lines = await _context.VendorReturnLine.AsNoTracking()
                .Where(l => noteIds.Contains(l.VendorReturnId))
                .ToListAsync(cancellationToken);

            var vendorNames = await _context.Vendor.AsNoTracking()
                .Where(v => notes.Select(n => n.VendorId).Distinct().Contains(v.VendorId))
                .ToDictionaryAsync(v => v.VendorId, v => v.VendorName, cancellationToken);

            var itemNames = await _context.InventoryItem.AsNoTracking()
                .Where(i => lines.Select(l => l.InventoryItemId).Distinct().Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, i => i.ItemName, cancellationToken);

            var result = notes.Select(n => new VendorReturnRow
            {
                VendorReturnId = n.VendorReturnId,
                ReturnNoteNo = n.ReturnNoteNo,
                VendorName = vendorNames.TryGetValue(n.VendorId, out var vn) ? vn : null,
                TotalQty = n.TotalQty,
                TotalValue = n.TotalValue,
                GeneratedAt = n.GeneratedAt,
                GeneratedBy = n.GeneratedBy,
                Lines = lines.Where(l => l.VendorReturnId == n.VendorReturnId)
                    .Select(l => new VendorReturnLineRow
                    {
                        ItemName = itemNames.TryGetValue(l.InventoryItemId, out var itemName) ? itemName : "Unknown",
                        BatchNumber = l.BatchNumber,
                        ExpiryDate = l.ExpiryDate,
                        Qty = l.Qty,
                        UnitCost = l.UnitCost,
                        LineValue = l.LineValue,
                    }).ToList(),
            }).ToList();

            return new GetVendorReturnsResponseModel { Returns = result };
        }
    }
}
