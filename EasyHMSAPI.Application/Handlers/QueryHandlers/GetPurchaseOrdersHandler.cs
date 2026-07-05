using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPurchaseOrdersHandler :
        IRequestHandler<GetPurchaseOrdersRequestModel, GetPurchaseOrdersResponseModel>,
        IRequestHandler<GetPurchaseOrderDetailRequestModel, GetPurchaseOrderDetailResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPurchaseOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPurchaseOrdersResponseModel> Handle(GetPurchaseOrdersRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.PurchaseOrder.Where(p => p.HospitalId == request.HospitalId);
            if (!string.IsNullOrWhiteSpace(request.Status))
                query = query.Where(p => p.Status == request.Status.Trim().ToUpperInvariant());

            var pos = await query.OrderByDescending(p => p.OrderedAt).ToListAsync(cancellationToken);
            var vendorNames = await _context.Vendor
                .Where(v => pos.Select(p => p.VendorId).Distinct().Contains(v.VendorId))
                .ToDictionaryAsync(v => v.VendorId, v => v.VendorName, cancellationToken);
            var lineCounts = await _context.PurchaseOrderLine
                .Where(l => pos.Select(p => p.PurchaseOrderId).Contains(l.PurchaseOrderId))
                .GroupBy(l => l.PurchaseOrderId)
                .Select(g => new { PurchaseOrderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PurchaseOrderId, x => x.Count, cancellationToken);

            var result = pos.Select(p => new PurchaseOrderDataModel
            {
                PurchaseOrderId = p.PurchaseOrderId,
                PoNumber = p.PoNumber,
                VendorId = p.VendorId,
                VendorName = vendorNames.TryGetValue(p.VendorId, out var name) ? name : null,
                IndentId = p.IndentId,
                Status = p.Status,
                OrderedAt = p.OrderedAt,
                ExpectedDeliveryDate = p.ExpectedDeliveryDate,
                LineCount = lineCounts.TryGetValue(p.PurchaseOrderId, out var count) ? count : 0,
            }).ToList();

            return new GetPurchaseOrdersResponseModel { PurchaseOrders = result };
        }

        public async Task<GetPurchaseOrderDetailResponseModel> Handle(GetPurchaseOrderDetailRequestModel request, CancellationToken cancellationToken)
        {
            var po = await _context.PurchaseOrder.FirstOrDefaultAsync(
                p => p.PurchaseOrderId == request.PurchaseOrderId && p.HospitalId == request.HospitalId, cancellationToken);
            if (po == null)
                return new GetPurchaseOrderDetailResponseModel { Success = false, Message = "Purchase order not found." };

            var vendorName = await _context.Vendor
                .Where(v => v.VendorId == po.VendorId)
                .Select(v => v.VendorName)
                .FirstOrDefaultAsync(cancellationToken);

            var lines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == po.PurchaseOrderId).ToListAsync(cancellationToken);
            var itemsById = await _context.InventoryItem
                .Where(x => lines.Select(l => l.InventoryItemId).Contains(x.InventoryItemId))
                .ToDictionaryAsync(x => x.InventoryItemId, cancellationToken);

            return new GetPurchaseOrderDetailResponseModel
            {
                Success = true,
                PurchaseOrder = new PurchaseOrderDataModel
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    PoNumber = po.PoNumber,
                    VendorId = po.VendorId,
                    VendorName = vendorName,
                    IndentId = po.IndentId,
                    Status = po.Status,
                    OrderedAt = po.OrderedAt,
                    ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                    LineCount = lines.Count,
                },
                Lines = lines.Select(l => new PurchaseOrderLineDataModel
                {
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    InventoryItemId = l.InventoryItemId,
                    ItemName = itemsById.TryGetValue(l.InventoryItemId, out var item) ? item.ItemName : "Unknown",
                    Unit = itemsById.TryGetValue(l.InventoryItemId, out var item2) ? item2.Unit : "",
                    Qty = l.Qty,
                    Rate = l.Rate,
                    ReceivedQty = l.ReceivedQty,
                }).ToList(),
            };
        }
    }
}
