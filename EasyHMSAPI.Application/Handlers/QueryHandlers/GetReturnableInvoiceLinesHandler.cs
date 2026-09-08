using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Bill-scan → returnable lines. Built from InventoryMovement (ISSUE rows for the invoice's
    // Encounter) rather than BillingChargeEvent, since InventoryMovement is the only place that
    // carries per-batch qty/expiry for a FEFO-split dispense — BillingChargeEvent only has one
    // merged qty/price per cart line. Pricing (UnitPrice) is looked up from the matching charge
    // event by InventoryItemId (via ChargeId -> InventoryItem), a soft join — there's no direct
    // ChargeEventId column on InventoryMovement for pharmacy sales today (see PHARMACY_PRD notes).
    public class GetReturnableInvoiceLinesHandler : IRequestHandler<GetReturnableInvoiceLinesRequestModel, GetReturnableInvoiceLinesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetReturnableInvoiceLinesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetReturnableInvoiceLinesResponseModel> Handle(GetReturnableInvoiceLinesRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InvoiceNo))
                return new GetReturnableInvoiceLinesResponseModel { Found = false, Message = "Invoice number is required." };

            var invoice = await _context.BillingInvoice.AsNoTracking()
                .FirstOrDefaultAsync(i => i.HospitalId == request.HospitalId && i.InvoiceNo == request.InvoiceNo.Trim(), cancellationToken);
            if (invoice == null)
                return new GetReturnableInvoiceLinesResponseModel { Found = false, Message = "Invoice not found." };

            var movements = await _context.InventoryMovement.AsNoTracking()
                .Where(m => m.HospitalId == request.HospitalId && m.EncounterId == invoice.EncounterId && m.MovementType == "ISSUE" && m.BatchId != null)
                .ToListAsync(cancellationToken);

            if (movements.Count == 0)
                return new GetReturnableInvoiceLinesResponseModel
                {
                    Found = true, InvoiceId = invoice.InvoiceId, InvoiceNo = invoice.InvoiceNo,
                    EncounterId = invoice.EncounterId, PatientId = invoice.PatientId, InvoiceDate = invoice.InvoiceDate,
                    Message = "No dispensed batch lines found for this invoice.",
                };

            var itemIds = movements.Select(m => m.InventoryItemId).Distinct().ToList();
            var items = await _context.InventoryItem.AsNoTracking()
                .Where(i => itemIds.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, cancellationToken);

            var chargeIds = items.Values.Where(i => i.ChargeId.HasValue).Select(i => i.ChargeId!.Value).Distinct().ToList();
            var chargeEvents = chargeIds.Count == 0
                ? new List<Domain.Entities.BillingChargeEvent>()
                : await _context.BillingChargeEvent.AsNoTracking()
                    .Where(c => c.EncounterId == invoice.EncounterId && c.StatusCode != BillingConstants.ChargeEventStatus.Void
                             && c.ChargeId.HasValue && chargeIds.Contains(c.ChargeId.Value))
                    .ToListAsync(cancellationToken);
            // ChargeId -> ChargeEvent (first match; a single checkout posts at most one line per
            // distinct ChargeId, so this is exact for the pharmacy checkout flow).
            var chargeByChargeId = chargeEvents
                .Where(c => c.ChargeId.HasValue)
                .GroupBy(c => c.ChargeId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var batchIds = movements.Select(m => m.BatchId!.Value).Distinct().ToList();
            var batches = await _context.Batch.AsNoTracking()
                .Where(b => batchIds.Contains(b.BatchId))
                .ToDictionaryAsync(b => b.BatchId, cancellationToken);

            // Already-returned qty per (ChargeEventId, BatchId), so a second partial return against
            // the same line/batch can't exceed what's left.
            var priorReturns = await _context.PharmacyReturnLine.AsNoTracking()
                .Where(l => batchIds.Contains(l.BatchId))
                .GroupBy(l => new { l.ChargeEventId, l.BatchId })
                .Select(g => new { g.Key.ChargeEventId, g.Key.BatchId, Qty = g.Sum(x => x.ReturnedQty) })
                .ToListAsync(cancellationToken);

            var today = DateTime.UtcNow.Date;
            var lines = new List<ReturnableLineRow>();

            foreach (var m in movements)
            {
                if (!items.TryGetValue(m.InventoryItemId, out var item)) continue;
                if (!batches.TryGetValue(m.BatchId!.Value, out var batch)) continue;

                Guid chargeEventId = Guid.Empty;
                decimal unitPrice = 0;
                if (item.ChargeId.HasValue && chargeByChargeId.TryGetValue(item.ChargeId.Value, out var ce))
                {
                    chargeEventId = ce.ChargeEventId;
                    unitPrice = ce.Qty > 0 ? Math.Round(ce.NetAmount / ce.Qty, 4) : ce.UnitPrice;
                }

                var alreadyReturned = priorReturns
                    .Where(p => p.ChargeEventId == chargeEventId && p.BatchId == batch.BatchId)
                    .Sum(p => p.Qty);

                var returnable = m.Qty - alreadyReturned;
                if (returnable <= 0) continue;

                lines.Add(new ReturnableLineRow
                {
                    ChargeEventId = chargeEventId,
                    InventoryItemId = item.InventoryItemId,
                    ItemName = item.ItemName,
                    BatchId = batch.BatchId,
                    BatchNumber = batch.BatchNumber,
                    ExpiryDate = batch.ExpiryDate,
                    IsExpired = batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < today,
                    DispensedQty = m.Qty,
                    AlreadyReturnedQty = alreadyReturned,
                    ReturnableQty = returnable,
                    UnitPrice = unitPrice,
                });
            }

            return new GetReturnableInvoiceLinesResponseModel
            {
                Found = true,
                InvoiceId = invoice.InvoiceId,
                InvoiceNo = invoice.InvoiceNo,
                EncounterId = invoice.EncounterId,
                PatientId = invoice.PatientId,
                InvoiceDate = invoice.InvoiceDate,
                Lines = lines,
            };
        }
    }
}
