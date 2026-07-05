using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetGoodsReceiptNotesHandler : IRequestHandler<GetGoodsReceiptNotesRequestModel, GetGoodsReceiptNotesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetGoodsReceiptNotesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetGoodsReceiptNotesResponseModel> Handle(GetGoodsReceiptNotesRequestModel request, CancellationToken cancellationToken)
        {
            var grns = await _context.GoodsReceiptNote
                .Where(g => g.HospitalId == request.HospitalId)
                .OrderByDescending(g => g.ReceivedAt)
                .ToListAsync(cancellationToken);

            var poNumbers = await _context.PurchaseOrder
                .Where(p => grns.Select(g => g.PurchaseOrderId).Distinct().Contains(p.PurchaseOrderId))
                .ToDictionaryAsync(p => p.PurchaseOrderId, p => p.PoNumber, cancellationToken);
            var vendorNames = await _context.Vendor
                .Where(v => grns.Select(g => g.VendorId).Distinct().Contains(v.VendorId))
                .ToDictionaryAsync(v => v.VendorId, v => v.VendorName, cancellationToken);
            var storeNames = await _context.Store
                .Where(s => grns.Select(g => g.ReceivedStoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var result = grns.Select(g => new GoodsReceiptNoteDataModel
            {
                GrnId = g.GrnId,
                GrnNumber = g.GrnNumber,
                PurchaseOrderId = g.PurchaseOrderId,
                PoNumber = poNumbers.TryGetValue(g.PurchaseOrderId, out var poNo) ? poNo : null,
                VendorId = g.VendorId,
                VendorName = vendorNames.TryGetValue(g.VendorId, out var vName) ? vName : null,
                ReceivedStoreName = storeNames.TryGetValue(g.ReceivedStoreId, out var sName) ? sName : null,
                InvoiceNumber = g.InvoiceNumber,
                InvoiceAmount = g.InvoiceAmount,
                MatchStatus = g.MatchStatus,
                ReceivedAt = g.ReceivedAt,
            }).ToList();

            return new GetGoodsReceiptNotesResponseModel { GoodsReceiptNotes = result };
        }
    }
}
