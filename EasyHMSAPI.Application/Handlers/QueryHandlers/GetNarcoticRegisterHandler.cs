using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetNarcoticRegisterHandler : IRequestHandler<GetNarcoticRegisterRequestModel, GetNarcoticRegisterResponseModel>
    {
        private readonly AppDbContext _context;

        public GetNarcoticRegisterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNarcoticRegisterResponseModel> Handle(GetNarcoticRegisterRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.NarcoticRegisterEntry.Where(n => n.HospitalId == request.HospitalId);
            if (request.InventoryItemId.HasValue && request.InventoryItemId != Guid.Empty)
                query = query.Where(n => n.InventoryItemId == request.InventoryItemId);
            if (!string.IsNullOrWhiteSpace(request.FormType))
                query = query.Where(n => n.FormType == request.FormType);

            var entries = await query.OrderByDescending(n => n.RecordedAt).Take(200).ToListAsync(cancellationToken);

            var itemNames = await _context.InventoryItem
                .Where(i => entries.Select(e => e.InventoryItemId).Distinct().Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, i => i.ItemName, cancellationToken);
            var batchNumbers = await _context.Batch
                .Where(b => entries.Select(e => e.BatchId).Distinct().Contains(b.BatchId))
                .ToDictionaryAsync(b => b.BatchId, b => b.BatchNumber, cancellationToken);
            var storeNames = await _context.Store
                .Where(s => entries.Select(e => e.StoreId).Distinct().Contains(s.StoreId))
                .ToDictionaryAsync(s => s.StoreId, s => s.StoreName, cancellationToken);

            var result = entries.Select(e => new NarcoticRegisterEntryDataModel
            {
                RegisterEntryId = e.RegisterEntryId,
                ItemName = itemNames.TryGetValue(e.InventoryItemId, out var itemName) ? itemName : "Unknown",
                BatchNumber = batchNumbers.TryGetValue(e.BatchId, out var batchNo) ? batchNo : null,
                StoreName = storeNames.TryGetValue(e.StoreId, out var storeName) ? storeName : null,
                FormType = e.FormType,
                Direction = e.Direction,
                Qty = e.Qty,
                BalanceAfter = e.BalanceAfter,
                PatientId = e.PatientId,
                PrescriberRef = e.PrescriberRef,
                IssuedBy = e.IssuedBy,
                WitnessBy = e.WitnessBy,
                RecordedAt = e.RecordedAt,
            }).ToList();

            return new GetNarcoticRegisterResponseModel { Entries = result };
        }
    }
}
