using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetNearExpiryReportHandler : IRequestHandler<GetNearExpiryReportRequestModel, GetNearExpiryReportResponseModel>
    {
        private readonly AppDbContext _context;

        public GetNearExpiryReportHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNearExpiryReportResponseModel> Handle(GetNearExpiryReportRequestModel request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            var query = _context.Batch.Where(b => b.HospitalId == request.HospitalId
                && b.Status == "ACTIVE" && b.RemainingQty > 0 && b.ExpiryDate != null);

            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                query = query.Where(b => b.StoreId == request.StoreId);
            if (request.VendorId.HasValue && request.VendorId != Guid.Empty)
                query = query.Where(b => b.VendorId == request.VendorId);

            // Only batches inside the 180-day watch window — matches the Green cutoff, so we never
            // pull the entire (mostly Green, mostly irrelevant) stock position for this report.
            var windowEnd = today.AddDays(180);
            query = query.Where(b => b.ExpiryDate!.Value.Date < windowEnd);

            var batches = await query.ToListAsync(cancellationToken);

            var itemIds = batches.Select(b => b.InventoryItemId).Distinct().ToList();
            var items = await _context.InventoryItem
                .Where(i => itemIds.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, cancellationToken);
            var storeNames = await _context.Store
                .Where(s => batches.Select(b => b.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);
            var vendorIds = batches.Where(b => b.VendorId.HasValue).Select(b => b.VendorId!.Value).Distinct().ToList();
            var vendorNames = vendorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Vendor.Where(v => vendorIds.Contains(v.VendorId)).ToDictionaryAsync(v => v.VendorId, v => v.VendorName, cancellationToken);

            var result = batches.Select(b =>
            {
                var bucket = ExpiryBucketCalculator.Compute(b.ExpiryDate, today);
                items.TryGetValue(b.InventoryItemId, out var item);
                return new NearExpiryBatchDataModel
                {
                    BatchId = b.BatchId,
                    InventoryItemId = b.InventoryItemId,
                    ItemName = item?.ItemName,
                    GenericName = item?.GenericName,
                    StoreId = b.StoreId,
                    StoreName = storeNames.TryGetValue(b.StoreId, out var sn) ? sn : null,
                    VendorId = b.VendorId,
                    VendorName = b.VendorId.HasValue && vendorNames.TryGetValue(b.VendorId.Value, out var vn) ? vn : null,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    DaysToExpiry = b.ExpiryDate.HasValue ? (int)(b.ExpiryDate.Value.Date - today).TotalDays : null,
                    Bucket = bucket,
                    RemainingQty = b.RemainingQty,
                    Mrp = b.Mrp,
                };
            })
            .Where(m => string.IsNullOrWhiteSpace(request.Bucket) || string.Equals(m.Bucket, request.Bucket, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.DaysToExpiry)
            .ToList();

            return new GetNearExpiryReportResponseModel { Batches = result };
        }
    }
}
