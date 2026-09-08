using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDrugScheduleRegisterHandler : IRequestHandler<GetDrugScheduleRegisterRequestModel, GetDrugScheduleRegisterResponseModel>
    {
        private readonly AppDbContext _context;

        public GetDrugScheduleRegisterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDrugScheduleRegisterResponseModel> Handle(GetDrugScheduleRegisterRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.DrugScheduleRegisterEntry.Where(e => e.HospitalId == request.HospitalId);
            if (request.InventoryItemId.HasValue && request.InventoryItemId != Guid.Empty)
                query = query.Where(e => e.InventoryItemId == request.InventoryItemId);
            if (!string.IsNullOrWhiteSpace(request.ScheduleClass))
                query = query.Where(e => e.ScheduleClass == request.ScheduleClass);

            var entries = await query.OrderByDescending(e => e.RecordedAt).Take(200).ToListAsync(cancellationToken);

            var itemNames = await _context.InventoryItem
                .Where(i => entries.Select(e => e.InventoryItemId).Distinct().Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, i => i.ItemName, cancellationToken);
            var batchNumbers = await _context.Batch
                .Where(b => entries.Select(e => e.BatchId).Distinct().Contains(b.BatchId))
                .ToDictionaryAsync(b => b.BatchId, b => b.BatchNumber, cancellationToken);
            var storeNames = await _context.Store
                .Where(s => entries.Select(e => e.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var result = entries.Select(e => new DrugScheduleRegisterEntryDataModel
            {
                RegisterEntryId = e.RegisterEntryId,
                ItemName = itemNames.TryGetValue(e.InventoryItemId, out var itemName) ? itemName : "Unknown",
                BatchNumber = batchNumbers.TryGetValue(e.BatchId, out var batchNo) ? batchNo : null,
                StoreName = storeNames.TryGetValue(e.StoreId, out var storeName) ? storeName : null,
                ScheduleClass = e.ScheduleClass,
                Qty = e.Qty,
                PatientId = e.PatientId,
                PrescriberRef = e.PrescriberRef,
                DispensedBy = e.DispensedBy,
                RecordedAt = e.RecordedAt,
            }).ToList();

            return new GetDrugScheduleRegisterResponseModel { Entries = result };
        }
    }
}
